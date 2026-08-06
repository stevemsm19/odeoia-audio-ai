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
        private readonly IAudioProcessingLauncher _audioProcessingLauncher;
        private readonly IAudioRepository _audioRepository;

        private static readonly string[] AllowedExtensions = { ".mp3", ".wav" };

        public AudioController(UploadAudioUseCase uploadAudioUseCase, IAudioProcessingLauncher audioProcessingLauncher, IAudioRepository audioRepository)
        {
            _uploadAudioUseCase = uploadAudioUseCase ?? throw new ArgumentNullException(nameof(uploadAudioUseCase));
            _audioProcessingLauncher = audioProcessingLauncher ?? throw new ArgumentNullException(nameof(audioProcessingLauncher));
            _audioRepository = audioRepository ?? throw new ArgumentNullException(nameof(audioRepository));
        }

        /// <summary>
        /// Uploads an audio file (.mp3 or .wav) to Supabase storage, records its metadata in the database,
        /// launches its two background processing flows, and returns immediately.
        /// </summary>
        [HttpPost("upload")]
        [ProducesResponseType(typeof(AudioUploadResponse), StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<IActionResult> UploadAudioFile(IFormFile file, CancellationToken cancellationToken)
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
                cancellationToken
            );

            // 2. Launch the two independent processing flows (Compression, Transcription→Summary),
            // one explicit Thread per flow, and return immediately — they update their own state.
            string correlationId = HttpContext.TraceIdentifier;
            _audioProcessingLauncher.LaunchProcessing(audioRecord.Id, correlationId);

            var response = new AudioUploadResponse(
                audioRecord.Id,
                audioRecord.FileName,
                audioRecord.StorageUrl,
                audioRecord.Status.ToString(),
                audioRecord.CreatedAt);

            // 3. Return HTTP 202 Accepted to signal that background processing has started
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
                record.TranscriptionStatus.ToString(),
                record.TranscriptionError,
                record.SummaryStatus.ToString(),
                record.SummaryStorageUrl,
                record.SummaryError));
        }
    }

    public record AudioUploadResponse(Guid Id, string FileName, string StorageUrl, string Status, DateTime CreatedAt);

    public record AudioProcessingStatusResponse(
        Guid Id,
        string FileName,
        string OverallStatus,
        string CompressionStatus,
        string? CompressedStorageUrl,
        string TranscriptionStatus,
        string? TranscriptionError,
        string SummaryStatus,
        string? SummaryStorageUrl,
        string? SummaryError);
}
