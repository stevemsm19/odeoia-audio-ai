using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
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

        private static readonly string[] AllowedExtensions = { ".mp3", ".wav" };
        private static readonly string[] AllowedContentTypes = { "audio/mpeg", "audio/wav", "audio/wave", "audio/x-wav" };

        public AudioController(UploadAudioUseCase uploadAudioUseCase)
        {
            _uploadAudioUseCase = uploadAudioUseCase;
        }

        /// <summary>
        /// Uploads an audio file (.mp3 or .wav) to Supabase storage and records its metadata in the database.
        /// </summary>
        [HttpPost("upload")]
        [ProducesResponseType(typeof(AudioUploadResponse), StatusCodes.Status201Created)]
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

            var audioRecord = await _uploadAudioUseCase.ExecuteAsync(
                fileStream,
                file.FileName,
                file.ContentType,
                cancellationToken
            );

            var response = new AudioUploadResponse(audioRecord.Id, audioRecord.FileName, audioRecord.StorageUrl, audioRecord.CreatedAt);

            return CreatedAtAction(nameof(UploadAudioFile), response);
        }
    }

    public record AudioUploadResponse(Guid Id, string FileName, string StorageUrl, DateTime CreatedAt);
}
