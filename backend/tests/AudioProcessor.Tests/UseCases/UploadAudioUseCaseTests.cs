using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AudioProcessor.Application.Interfaces;
using AudioProcessor.Application.UseCases;
using AudioProcessor.Domain.Entities;
using Moq;
using Xunit;

namespace AudioProcessor.Tests.UseCases
{
    public class UploadAudioUseCaseTests
    {
        private readonly Mock<IAudioStorageService> _mockStorageService;
        private readonly Mock<IAudioRepository> _mockAudioRepository;
        private readonly UploadAudioUseCase _useCase;

        public UploadAudioUseCaseTests()
        {
            _mockStorageService = new Mock<IAudioStorageService>();
            _mockAudioRepository = new Mock<IAudioRepository>();
            _useCase = new UploadAudioUseCase(_mockStorageService.Object, _mockAudioRepository.Object);
        }

        [Fact]
        public async Task ExecuteAsync_WithValidAudioFile_ReturnsAudioRecord()
        {
            // Arrange
            const string expectedUrl = "https://example.supabase.co/storage/v1/object/public/audio-files/test.mp3";
            const string fileName = "test.mp3";
            const string contentType = "audio/mpeg";

            using var fakeStream = new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });

            _mockStorageService
                .Setup(s => s.UploadAudioAsync(It.IsAny<Stream>(), fileName, contentType, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedUrl);

            _mockAudioRepository
                .Setup(r => r.SaveAudioRecordAsync(It.IsAny<AudioRecord>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            AudioRecord result = await _useCase.ExecuteAsync(fakeStream, fileName, contentType);

            // Assert
            Assert.NotNull(result);
            Assert.NotEqual(Guid.Empty, result.Id);
            Assert.Equal(expectedUrl, result.StorageUrl);
            Assert.Equal(fileName, result.FileName);
        }

        [Fact]
        public async Task ExecuteAsync_WithNullStream_ThrowsArgumentException()
        {
            // Arrange
            Stream? nullStream = null;

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _useCase.ExecuteAsync(nullStream!, "file.mp3", "audio/mpeg"));
        }

        [Fact]
        public async Task ExecuteAsync_WithEmptyFileName_ThrowsArgumentException()
        {
            // Arrange
            using var fakeStream = new MemoryStream(new byte[] { 1, 2, 3 });

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentException>(() =>
                _useCase.ExecuteAsync(fakeStream, string.Empty, "audio/mpeg"));
        }

        [Fact]
        public async Task ExecuteAsync_CallsStorageServiceAndRepository()
        {
            // Arrange
            const string fileName = "audio.wav";
            const string contentType = "audio/wav";
            const string returnedUrl = "https://example.supabase.co/audio.wav";

            using var fakeStream = new MemoryStream(new byte[] { 10, 20, 30 });

            _mockStorageService
                .Setup(s => s.UploadAudioAsync(It.IsAny<Stream>(), fileName, contentType, It.IsAny<CancellationToken>()))
                .ReturnsAsync(returnedUrl);

            _mockAudioRepository
                .Setup(r => r.SaveAudioRecordAsync(It.IsAny<AudioRecord>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _useCase.ExecuteAsync(fakeStream, fileName, contentType);

            // Assert
            _mockStorageService.Verify(
                s => s.UploadAudioAsync(It.IsAny<Stream>(), fileName, contentType, It.IsAny<CancellationToken>()),
                Times.Once
            );

            _mockAudioRepository.Verify(
                r => r.SaveAudioRecordAsync(It.IsAny<AudioRecord>(), It.IsAny<CancellationToken>()),
                Times.Once
            );
        }
    }
}
