using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using AudioProcessor.Application.Interfaces;
using AudioProcessor.Application.UseCases;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AudioProcessor.Api.Controllers
{
    /// <summary>
    /// Handles incoming HTTP requests for audio processing.
    /// Exposes endpoints for uploading raw audio files and launching their background processing flows.
    /// </summary>
    [ApiController]
    [Route("api/audio")]
    public class AudioController : ControllerBase
    {
        private readonly UploadAudioUseCase _uploadAudioUseCase;
        private readonly IAudioRepository _audioRepository;

        private static readonly string[] AllowedExtensions = { ".mp3", ".wav" };

        public AudioController(UploadAudioUseCase uploadAudioUseCase, IAudioRepository audioRepository)
        {
            _uploadAudioUseCase = uploadAudioUseCase ?? throw new ArgumentNullException(nameof(uploadAudioUseCase));
            _audioRepository = audioRepository ?? throw new ArgumentNullException(nameof(audioRepository));
        }

        /// <summary>
        /// Uploads an audio file (.mp3 or .wav) to Supabase storage, records its metadata in the
        /// database as <c>Pending</c>, and returns immediately — the background processing
        /// service picks it up and runs its two processing flows asynchronously.
        /// </summary>
        [HttpPost("upload")]
        [ProducesResponseType(typeof(AudioUploadResponse), StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UploadAudioFile(IFormFile file, CancellationToken cancellationToken, [FromForm] string? email = null)
        {
            if (file is null || file.Length == 0)
                return BadRequest(new { error = "No file was provided or the file is empty." });

            string fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!AllowedExtensions.Contains(fileExtension))
                return BadRequest(new { error = $"Invalid file type. Only {string.Join(", ", AllowedExtensions)} files are allowed." });

            await using Stream fileStream = file.OpenReadStream();

            // 1. Store original file and record in database with initial status (Pending)
            var audioRecord = await _uploadAudioUseCase.ExecuteAsync(
                fileStream,
                file.FileName,
                file.ContentType,
                userEmail: email,
                cancellationToken: cancellationToken
            );

            // 2. The record stays Pending — the background processing service claims it
            // and runs its two processing flows on its own schedule.
            var response = new AudioUploadResponse(
                audioRecord.Id,
                audioRecord.FileName,
                audioRecord.StorageUrl,
                audioRecord.Status.ToString(),
                audioRecord.CreatedAt);

            // 3. Return HTTP 202 Accepted to signal that the upload was accepted for processing
            return Accepted(response);
        }

        /// <summary>
        /// Returns per-stage status plus <see cref="AudioProcessingStatusResponse.OverallStatus"/> —
        /// never Failed, used only by the frontend to decide when to stop polling.
        /// </summary>
        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(AudioProcessingStatusResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetAudioStatus(Guid id, CancellationToken cancellationToken)
        {
            var record = await _audioRepository.GetByIdAsync(id, cancellationToken);
            if (record is null)
                return NotFound();

            return Ok(new AudioProcessingStatusResponse(
                record.Id,
                record.FileName,
                record.OverallStatus.ToString(),
                record.Status.ToString(),
                record.CompressedStorageUrl,
                record.CompressionDurationMs,
                record.TranscriptionStatus.ToString(),
                record.TranscriptionError,
                record.TranscriptionDurationMs,
                record.SummaryStatus.ToString(),
                record.SummaryStorageUrl,
                record.SummaryError,
                record.SummaryDurationMs,
                record.NoiseReductionStatus.ToString(),
                record.FilteredStorageUrl,
                record.NoiseReductionError,
                record.NoiseReductionDurationMs));
        }

        /// <summary>
        /// Lists every audio record associated with the given user email, newest first.
        /// Used by the "List Audios" frontend section.
        /// </summary>
        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<AudioListItemResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> ListAudios([FromQuery] string? email, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(email))
                return BadRequest(new { error = "The 'email' query parameter is required." });

            string normalizedEmail = email.Trim().ToLowerInvariant();
            var records = await _audioRepository.GetByUserEmailAsync(normalizedEmail, cancellationToken);

            var response = records.Select(r => new AudioListItemResponse(
                r.Id,
                r.FileName,
                r.StorageUrl,
                r.CompressedStorageUrl,
                r.OverallStatus.ToString(),
                r.SummaryStorageUrl,
                r.CreatedAt,
                r.FilteredStorageUrl));

            return Ok(response);
        }
    }

    public record AudioUploadResponse(Guid Id, string FileName, string StorageUrl, string Status, DateTime CreatedAt);

    public record AudioListItemResponse(
        Guid Id,
        string FileName,
        string StorageUrl,
        string? CompressedStorageUrl,
        string OverallStatus,
        string? SummaryStorageUrl,
        DateTime CreatedAt,
        string? FilteredStorageUrl);

    public record AudioProcessingStatusResponse(
        Guid Id,
        string FileName,
        string OverallStatus,
        string CompressionStatus,
        string? CompressedStorageUrl,
        long? CompressionDurationMs,
        string TranscriptionStatus,
        string? TranscriptionError,
        long? TranscriptionDurationMs,
        string SummaryStatus,
        string? SummaryStorageUrl,
        string? SummaryError,
        long? SummaryDurationMs,
        string NoiseReductionStatus,
        string? FilteredStorageUrl,
        string? NoiseReductionError,
        long? NoiseReductionDurationMs);
}
