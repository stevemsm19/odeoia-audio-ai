using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AudioProcessor.Application.Interfaces;
using AudioProcessor.Domain.Entities;

namespace AudioProcessor.Application.UseCases
{
    /// <summary>
    /// Coordinates the upload workflow for raw audio files.
    /// This use case handles transferring the file stream to the cloud/external storage,
    /// generating a tracking record in the persistent repository, and returning the initialized domain entity.
    /// </summary>
    public class UploadAudioUseCase
    {
        private readonly IAudioStorageService _audioStorageService;
        private readonly IAudioRepository _audioRepository;

        public UploadAudioUseCase(IAudioStorageService audioStorageService, IAudioRepository audioRepository)
        {
            _audioStorageService = audioStorageService;
            _audioRepository = audioRepository;
        }

        /// <summary>
        /// Executes the upload and registration flow.
        /// </summary>
        /// <param name="fileStream">The stream containing raw audio data.</param>
        /// <param name="fileName">The original file name of the audio.</param>
        /// <param name="contentType">The media/MIME type of the audio file.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A newly created <see cref="AudioRecord"/> indicating successful upload and registration.</returns>
        public async Task<AudioRecord> ExecuteAsync(Stream fileStream, string fileName, string contentType, CancellationToken cancellationToken = default)
        {
            if (fileStream is null || fileStream.Length == 0)
                throw new ArgumentException("Audio file stream cannot be null or empty.", nameof(fileStream));

            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("File name cannot be null or empty.", nameof(fileName));

            Guid audioRecordId = Guid.NewGuid();

            string publicStorageUrl = await _audioStorageService.UploadAudioAsync(
                fileStream,
                fileName,
                contentType,
                audioRecordId,
                cancellationToken);

            var audioRecord = new AudioRecord(
                id: audioRecordId,
                storageUrl: publicStorageUrl,
                fileName: fileName,
                createdAt: DateTime.UtcNow
            );

            await _audioRepository.SaveAudioRecordAsync(audioRecord, cancellationToken);

            return audioRecord;
        }
    }
}
