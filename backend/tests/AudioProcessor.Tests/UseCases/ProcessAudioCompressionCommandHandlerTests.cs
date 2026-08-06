using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AudioProcessor.Application.Interfaces;
using AudioProcessor.Application.UseCases;
using AudioProcessor.Domain.Entities;
using AudioProcessor.Domain.Enums;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace AudioProcessor.Tests.UseCases
{
    public class ProcessAudioCompressionCommandHandlerTests
    {
        private readonly Mock<IAudioRepository> _mockAudioRepository;
        private readonly Mock<IAudioStorageService> _mockStorageService;
        private readonly Mock<IAudioCompressionService> _mockCompressionService;
        private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
        private readonly Mock<ILogger<ProcessAudioCompressionCommandHandler>> _mockLogger;
        private readonly HttpClient _httpClient;
        private readonly ProcessAudioCompressionCommandHandler _handler;

        public ProcessAudioCompressionCommandHandlerTests()
        {
            _mockAudioRepository = new Mock<IAudioRepository>();
            _mockStorageService = new Mock<IAudioStorageService>();
            _mockCompressionService = new Mock<IAudioCompressionService>();
            _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            _mockLogger = new Mock<ILogger<ProcessAudioCompressionCommandHandler>>();

            // Instantiate HttpClient using the mocked handler
            _httpClient = new HttpClient(_mockHttpMessageHandler.Object);

            _handler = new ProcessAudioCompressionCommandHandler(
                _mockAudioRepository.Object,
                _mockStorageService.Object,
                _mockCompressionService.Object,
                _httpClient,
                _mockLogger.Object
            );
        }

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

            // Mock HttpClient download response
            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StreamContent(new MemoryStream(new byte[] { 1, 2, 3, 4, 5 }))
                });

            // Mock compression service
            var compressedStream = new MemoryStream(new byte[] { 10, 20, 30 });
            _mockCompressionService
                .Setup(c => c.CompressToAacAsync(It.IsAny<Stream>(), originalRecord.FileName, It.IsAny<CancellationToken>()))
                .ReturnsAsync(compressedStream);

            // Mock storage service upload
            const string expectedCompressedUrl = "https://example.supabase.co/audio-files/compressed.aac";
            _mockStorageService
                .Setup(s => s.UploadAudioAsync(It.IsAny<Stream>(), "original.aac", "audio/aac", It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedCompressedUrl);

            _mockAudioRepository
                .Setup(r => r.UpdateAudioRecordAsync(It.IsAny<AudioRecord>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            // Act
            await _handler.HandleAsync(recordId);

            // Assert
            Assert.Equal(AudioProcessingStatus.Completed, originalRecord.Status);
            Assert.Equal(expectedCompressedUrl, originalRecord.CompressedStorageUrl);
            Assert.NotNull(originalRecord.CompressionDurationMs);
            Assert.True(originalRecord.CompressionDurationMs >= 0);

            _mockAudioRepository.Verify(r => r.UpdateAudioRecordAsync(originalRecord, It.IsAny<CancellationToken>()), Times.Exactly(2));
            _mockStorageService.Verify(s => s.UploadAudioAsync(It.IsAny<Stream>(), "original.aac", "audio/aac", It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
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

            // Mock HttpClient download response
            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>()
                )
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StreamContent(new MemoryStream(new byte[] { 1, 2, 3, 4, 5 }))
                });

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
            await _handler.HandleAsync(recordId);

            // Verify status was transitioned to Failed
            Assert.Equal(AudioProcessingStatus.Failed, originalRecord.Status);
            Assert.Null(originalRecord.CompressedStorageUrl);
            Assert.NotNull(originalRecord.CompressionDurationMs);

            _mockAudioRepository.Verify(r => r.UpdateAudioRecordAsync(originalRecord, It.IsAny<CancellationToken>()), Times.Exactly(2));
        }
    }
}
