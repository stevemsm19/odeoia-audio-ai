using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AudioProcessor.Application.Interfaces;
using AudioProcessor.Application.UseCases;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AudioProcessor.Api.Controllers
{
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

        [HttpPost("upload")]
        [ProducesResponseType(typeof(AudioUploadResponse), StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public IActionResult UploadAudioFile(IFormFile file, [FromForm] string? email = null)
        {
            if (file is null || file.Length == 0)
                return BadRequest(new { error = "No file was provided or the file is empty." });

            string fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!AllowedExtensions.Contains(fileExtension))
                return BadRequest(new { error = $"Invalid file type. Only {string.Join(", ", AllowedExtensions)} files are allowed." });

            using Stream fileStream = file.OpenReadStream();

            var audioRecord = _uploadAudioUseCase.Execute(
                fileStream,
                file.FileName,
                file.ContentType,
                userEmail: email
            );

            var response = new AudioUploadResponse(
                audioRecord.Id,
                audioRecord.FileName,
                audioRecord.StorageUrl,
                audioRecord.Status.ToString(),
                audioRecord.CreatedAt);

            return Accepted(response);
        }

        [HttpGet("{id:guid}")]
        [ProducesResponseType(typeof(AudioProcessingStatusResponse), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public IActionResult GetAudioStatus(Guid id)
        {
            var record = _audioRepository.GetById(id);
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
                record.SummaryDurationMs));
        }

        [HttpGet]
        [ProducesResponseType(typeof(IEnumerable<AudioListItemResponse>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult ListAudios([FromQuery] string? email)
        {
            if (string.IsNullOrWhiteSpace(email))
                return BadRequest(new { error = "The 'email' query parameter is required." });

            string normalizedEmail = email.Trim().ToLowerInvariant();
            var records = _audioRepository.GetByUserEmail(normalizedEmail);

            var response = records.Select(r => new AudioListItemResponse(
                r.Id,
                r.FileName,
                r.StorageUrl,
                r.CompressedStorageUrl,
                r.OverallStatus.ToString(),
                r.SummaryStorageUrl,
                r.CreatedAt));

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
        DateTime CreatedAt);

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
        long? SummaryDurationMs);
}
