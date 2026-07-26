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
    /// Exposes endpoints for uploading raw audio files and enqueuing them for asynchronous background compression.
    /// </summary>
    [ApiController]
    [Route("api/audio")]
    public class AudioController : ControllerBase
    {
        private readonly UploadAudioUseCase _uploadAudioUseCase;
        private readonly IBackgroundJobQueue _backgroundJobQueue;

        private static readonly string[] AllowedExtensions = { ".mp3", ".wav" };
        private static readonly string[] AllowedContentTypes = { "audio/mpeg", "audio/wav", "audio/wave", "audio/x-wav" };

        public AudioController(UploadAudioUseCase uploadAudioUseCase, IBackgroundJobQueue backgroundJobQueue)
        {
            _uploadAudioUseCase = uploadAudioUseCase ?? throw new ArgumentNullException(nameof(uploadAudioUseCase));
            _backgroundJobQueue = backgroundJobQueue ?? throw new ArgumentNullException(nameof(backgroundJobQueue));
        }

        /// <summary>
        /// Uploads an audio file (.mp3 or .wav) to Supabase storage, records its metadata in the database, 
        /// enqueues it for asynchronous background compression, and returns immediately.
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

            // 2. Enqueue the Guid of the audio record into the background worker channel queue
            await _backgroundJobQueue.EnqueueJobAsync(audioRecord.Id, cancellationToken);

            var response = new AudioUploadResponse(
                audioRecord.Id,
                audioRecord.FileName,
                audioRecord.StorageUrl,
                audioRecord.Status.ToString(),
                audioRecord.CreatedAt);

            // 3. Return HTTP 202 Accepted to signal that processing has been queued
            return Accepted(response);
        }
    }

    public record AudioUploadResponse(Guid Id, string FileName, string StorageUrl, string Status, DateTime CreatedAt);
}
