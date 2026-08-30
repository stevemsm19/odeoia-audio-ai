using System;
using System.IO;
using AudioProcessor.Application.Interfaces;
using AudioProcessor.Domain.Entities;

namespace AudioProcessor.Application.UseCases
{
    public class UploadAudioUseCase
    {
        private readonly IAudioStorageService _audioStorageService;
        private readonly IAudioRepository _audioRepository;

        public UploadAudioUseCase(IAudioStorageService audioStorageService, IAudioRepository audioRepository)
        {
            _audioStorageService = audioStorageService;
            _audioRepository = audioRepository;
        }

        public AudioRecord Execute(
            Stream fileStream, string fileName, string contentType,
            string? userEmail = null)
        {
            if (fileStream is null || fileStream.Length == 0)
                throw new ArgumentException("Audio file stream cannot be null or empty.", nameof(fileStream));

            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("File name cannot be null or empty.", nameof(fileName));

            Guid audioRecordId = Guid.NewGuid();

            string publicStorageUrl = _audioStorageService.UploadAudio(
                fileStream,
                fileName,
                contentType,
                audioRecordId);

            var audioRecord = new AudioRecord(
                id: audioRecordId,
                storageUrl: publicStorageUrl,
                fileName: fileName,
                createdAt: DateTime.UtcNow,
                userEmail: userEmail
            );

            _audioRepository.SaveAudioRecord(audioRecord);

            return audioRecord;
        }
    }
}
