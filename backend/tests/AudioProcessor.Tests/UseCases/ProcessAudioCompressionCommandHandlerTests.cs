using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AudioProcessor.Application.Interfaces;
using AudioProcessor.Application.UseCases;
using AudioProcessor.Domain.Entities;
using AudioProcessor.Domain.Enums;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AudioProcessor.Tests.UseCases
{
    public class ProcessAudioCompressionCommandHandlerTests
    {
        private readonly Mock<IAudioRepository> _mockAudioRepository;
        private readonly Mock<IAudioStorageService> _mockStorageService;
        private readonly Mock<IAudioCompressionService> _mockCompressionService;
        private readonly Mock<IAudioNoiseReductionService> _mockNoiseReductionService;
        private readonly Mock<ILogger<ProcessAudioCompressionCommandHandler>> _mockLogger;
        private readonly ProcessAudioCompressionCommandHandler _handler;

        public ProcessAudioCompressionCommandHandlerTests()
        {
            _mockAudioRepository = new Mock<IAudioRepository>();
            _mockStorageService = new Mock<IAudioStorageService>();
            _mockCompressionService = new Mock<IAudioCompressionService>();
            _mockNoiseReductionService = new Mock<IAudioNoiseReductionService>();
            _mockLogger = new Mock<ILogger<ProcessAudioCompressionCommandHandler>>();

            _handler = new ProcessAudioCompressionCommandHandler(
                _mockAudioRepository.Object,
                _mockStorageService.Object,
                _mockCompressionService.Object,
                _mockNoiseReductionService.Object,
                _mockLogger.Object
            );
        }

        private static Stream OriginalAudioStream() => new MemoryStream(new byte[] { 1, 2, 3, 4, 5 });

        [Fact]
        public async Task HandleAsync_SuccessfulCompression_UpdatesRecordToCompleted()
        {
            // Arrange
            var recordId = Guid.NewGuid();
            var originalRecord = new AudioRecord(
                id: recordId,
                storageUrl: "https://example.supabase.co/audio-files/original.mp3",
                fileName: "original.mp3",
                createdAt: DateTime.UtcNow
            );

            _mockAudioRepository
                .Setup(r => r.GetByIdAsync(recordId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(originalRecord);

            // Mock compression service
            var compressedStream = new MemoryStream(new byte[] { 10, 20, 30 });
            _mockCompressionService
                .Setup(c => c.CompressToAacAsync(It.IsAny<Stream>(), originalRecord.FileName, It.IsAny<CancellationToken>()))
                .ReturnsAsync(compressedStream);

            // Mock storage service upload (compressed)
            const string expectedCompressedUrl = "https://example.supabase.co/audio-files/compressed.aac";
            _mockStorageService
                .Setup(s => s.UploadAudioAsync(It.IsAny<Stream>(), "original.aac", "audio/aac", It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedCompressedUrl);

            // Mock noise reduction service and its upload (filtered)
            var filteredStream = new MemoryStream(new byte[] { 40, 50, 60 });
            _mockNoiseReductionService
                .Setup(n => n.ReduceNoiseAsync(It.IsAny<Stream>(), "original.aac", It.IsAny<CancellationToken>()))
                .ReturnsAsync(filteredStream);

            const string expectedFilteredUrl = "https://example.supabase.co/audio-files/filtered.aac";
            _mockStorageService
                .Setup(s => s.UploadAudioAsync(It.IsAny<Stream>(), "filtered.aac", "audio/aac", It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedFilteredUrl);

            _mockAudioRepository
                .Setup(r => r.UpdateAudioRecordAsync(It.IsAny<AudioRecord>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _handler.HandleAsync(recordId, OriginalAudioStream());

            // Assert
            Assert.Equal(AudioProcessingStatus.Completed, originalRecord.Status);
            Assert.Equal(expectedCompressedUrl, originalRecord.CompressedStorageUrl);
            Assert.NotNull(originalRecord.CompressionDurationMs);
            Assert.True(originalRecord.CompressionDurationMs >= 0);

            Assert.Equal(AudioNoiseReductionStatus.Completed, originalRecord.NoiseReductionStatus);
            Assert.Equal(expectedFilteredUrl, originalRecord.FilteredStorageUrl);
            Assert.NotNull(originalRecord.NoiseReductionDurationMs);

            _mockAudioRepository.Verify(r => r.UpdateAudioRecordAsync(originalRecord, It.IsAny<CancellationToken>()), Times.Exactly(4));
            _mockStorageService.Verify(s => s.UploadAudioAsync(It.IsAny<Stream>(), "original.aac", "audio/aac", It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
            _mockStorageService.Verify(s => s.UploadAudioAsync(It.IsAny<Stream>(), "filtered.aac", "audio/aac", It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_NoiseReductionFails_CompressionStaysCompletedAndNoiseReductionMarkedFailed()
        {
            // Arrange
            var recordId = Guid.NewGuid();
            var originalRecord = new AudioRecord(
                id: recordId,
                storageUrl: "https://example.supabase.co/audio-files/original.mp3",
                fileName: "original.mp3",
                createdAt: DateTime.UtcNow
            );

            _mockAudioRepository
                .Setup(r => r.GetByIdAsync(recordId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(originalRecord);

            var compressedStream = new MemoryStream(new byte[] { 10, 20, 30 });
            _mockCompressionService
                .Setup(c => c.CompressToAacAsync(It.IsAny<Stream>(), originalRecord.FileName, It.IsAny<CancellationToken>()))
                .ReturnsAsync(compressedStream);

            const string expectedCompressedUrl = "https://example.supabase.co/audio-files/compressed.aac";
            _mockStorageService
                .Setup(s => s.UploadAudioAsync(It.IsAny<Stream>(), "original.aac", "audio/aac", It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedCompressedUrl);

            _mockNoiseReductionService
                .Setup(n => n.ReduceNoiseAsync(It.IsAny<Stream>(), "original.aac", It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("FFmpeg afftdn failed."));

            _mockAudioRepository
                .Setup(r => r.UpdateAudioRecordAsync(It.IsAny<AudioRecord>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act — noise reduction failure must not rethrow or affect compression's own state.
            await _handler.HandleAsync(recordId, OriginalAudioStream());

            // Assert
            Assert.Equal(AudioProcessingStatus.Completed, originalRecord.Status);
            Assert.Equal(expectedCompressedUrl, originalRecord.CompressedStorageUrl);

            Assert.Equal(AudioNoiseReductionStatus.Failed, originalRecord.NoiseReductionStatus);
            Assert.Null(originalRecord.FilteredStorageUrl);
            Assert.NotNull(originalRecord.NoiseReductionDurationMs);
        }

        [Fact]
        public async Task HandleAsync_CompressionFails_UpdatesRecordToFailedWithoutThrowing()
        {
            // Arrange
            var recordId = Guid.NewGuid();
            var originalRecord = new AudioRecord(
                id: recordId,
                storageUrl: "https://example.supabase.co/audio-files/original.mp3",
                fileName: "original.mp3",
                createdAt: DateTime.UtcNow
            );

            _mockAudioRepository
                .Setup(r => r.GetByIdAsync(recordId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(originalRecord);

            // Make compression throw an exception
            _mockCompressionService
                .Setup(c => c.CompressToAacAsync(It.IsAny<Stream>(), originalRecord.FileName, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("FFmpeg failed to transcode."));

            _mockAudioRepository
                .Setup(r => r.UpdateAudioRecordAsync(It.IsAny<AudioRecord>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act — the handler fully handles and persists the failure itself; it must not
            // rethrow, otherwise AudioProcessingLauncher's outer catch would log the same
            // failure a second time.
            await _handler.HandleAsync(recordId, OriginalAudioStream());

            // Verify status was transitioned to Failed
            Assert.Equal(AudioProcessingStatus.Failed, originalRecord.Status);
            Assert.Null(originalRecord.CompressedStorageUrl);
            Assert.NotNull(originalRecord.CompressionDurationMs);

            _mockAudioRepository.Verify(r => r.UpdateAudioRecordAsync(originalRecord, It.IsAny<CancellationToken>()), Times.Exactly(2));
        }
    }
}
