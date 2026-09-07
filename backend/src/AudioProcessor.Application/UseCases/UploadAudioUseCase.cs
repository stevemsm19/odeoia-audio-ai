using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AudioProcessor.Application.Interfaces;
using AudioProcessor.Domain.Entities;

namespace AudioProcessor.Application.UseCases
{
    // Uploads the raw audio file to storage and creates its tracking record.
    public class UploadAudioUseCase
    {
        private readonly IAudioStorageService _audioStorageService;
        private readonly IAudioRepository _audioRepository;

        public UploadAudioUseCase(IAudioStorageService audioStorageService, IAudioRepository audioRepository)
        {
            _audioStorageService = audioStorageService;
            _audioRepository = audioRepository;
        }

        public async Task<AudioRecord> ExecuteAsync(
            Stream fileStream, string fileName, string contentType,
            string? userEmail = null, CancellationToken cancellationToken = default)
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
                createdAt: DateTime.UtcNow,
                userEmail: userEmail
            );

            await _audioRepository.SaveAudioRecordAsync(audioRecord, cancellationToken);

            return audioRecord;
        }

    }
}
