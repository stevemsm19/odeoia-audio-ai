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
    public class ProcessAudioSummaryCommandHandlerTests
    {
        private readonly Mock<IAudioRepository> _mockAudioRepository;
        private readonly Mock<IAudioTranscriptionService> _mockTranscriptionService;
        private readonly Mock<IAudioSummarizationService> _mockSummarizationService;
        private readonly Mock<IAudioStorageService> _mockStorageService;
        private readonly Mock<HttpMessageHandler> _mockHttpMessageHandler;
        private readonly HttpClient _httpClient;
        private readonly ProcessAudioSummaryCommandHandler _handler;

        public ProcessAudioSummaryCommandHandlerTests()
        {
            _mockAudioRepository = new Mock<IAudioRepository>();
            _mockTranscriptionService = new Mock<IAudioTranscriptionService>();
            _mockSummarizationService = new Mock<IAudioSummarizationService>();
            _mockStorageService = new Mock<IAudioStorageService>();
            _mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            _httpClient = new HttpClient(_mockHttpMessageHandler.Object);

            _mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StreamContent(new MemoryStream(new byte[] { 1, 2, 3 }))
                });

            _mockAudioRepository
                .Setup(r => r.UpdateAudioRecordAsync(It.IsAny<AudioRecord>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            _handler = new ProcessAudioSummaryCommandHandler(
                _mockAudioRepository.Object,
                _mockTranscriptionService.Object,
                _mockSummarizationService.Object,
                _mockStorageService.Object,
                _httpClient,
                Mock.Of<ILogger<ProcessAudioSummaryCommandHandler>>());
        }

        private static AudioRecord NewRecord() => new(
            id: Guid.NewGuid(),
            storageUrl: "https://example.supabase.co/audio-files/original.mp3",
            fileName: "original.mp3",
            createdAt: DateTime.UtcNow);

        [Fact]
        public async Task HandleAsync_HappyPath_TranscribesThenSummarizesThenUploads()
        {
            var record = NewRecord();
            _mockAudioRepository.Setup(r => r.GetByIdAsync(record.Id, It.IsAny<CancellationToken>())).ReturnsAsync(record);

            _mockTranscriptionService
                .Setup(s => s.TranscribeAsync(It.IsAny<Stream>(), record.FileName, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AudioTranscriptionResult("hello world", 100));

            _mockSummarizationService
                .Setup(s => s.SummarizeAsync("hello world", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AudioSummarizationResult("a short summary", 200));

            const string expectedUrl = "https://example.supabase.co/summaries/summary.txt";
            _mockStorageService
                .Setup(s => s.UploadAudioAsync(It.IsAny<Stream>(), "summary.txt", "text/plain", record.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expectedUrl);

            await _handler.HandleAsync(record.Id, "corr-1");

            Assert.Equal(AudioTranscriptionStatus.Completed, record.TranscriptionStatus);
            Assert.Equal(AudioSummaryStatus.Completed, record.SummaryStatus);
            Assert.Equal(expectedUrl, record.SummaryStorageUrl);
            _mockSummarizationService.Verify(s => s.SummarizeAsync("hello world", It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task HandleAsync_TranscriptionFails_MarksBothTranscriptionAndSummaryAsFailed_AndNeverSummarizes()
        {
            var record = NewRecord();
            _mockAudioRepository.Setup(r => r.GetByIdAsync(record.Id, It.IsAny<CancellationToken>())).ReturnsAsync(record);

            _mockTranscriptionService
                .Setup(s => s.TranscribeAsync(It.IsAny<Stream>(), record.FileName, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("whisper-cli exited with 1"));

            await _handler.HandleAsync(record.Id, "corr-2");

            Assert.Equal(AudioTranscriptionStatus.Failed, record.TranscriptionStatus);
            Assert.Equal(AudioSummaryStatus.Failed, record.SummaryStatus);
            _mockSummarizationService.Verify(s => s.SummarizeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
            _mockStorageService.Verify(s => s.UploadAudioAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task HandleAsync_SummarizationFails_TranscriptionStaysCompleted_SummaryMarkedFailed()
        {
            var record = NewRecord();
            _mockAudioRepository.Setup(r => r.GetByIdAsync(record.Id, It.IsAny<CancellationToken>())).ReturnsAsync(record);

            _mockTranscriptionService
                .Setup(s => s.TranscribeAsync(It.IsAny<Stream>(), record.FileName, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AudioTranscriptionResult("hello world", 100));

            _mockSummarizationService
                .Setup(s => s.SummarizeAsync("hello world", It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("llama-cli exited with 1"));

            await _handler.HandleAsync(record.Id, "corr-3");

            Assert.Equal(AudioTranscriptionStatus.Completed, record.TranscriptionStatus);
            Assert.Equal(AudioSummaryStatus.Failed, record.SummaryStatus);
        }

        [Fact]
        public async Task HandleAsync_SummaryAlreadyCompleted_SkipsProcessing()
        {
            var record = NewRecord();
            record.MarkSummaryAsCompleted("https://example.supabase.co/summaries/summary.txt", 10);
            _mockAudioRepository.Setup(r => r.GetByIdAsync(record.Id, It.IsAny<CancellationToken>())).ReturnsAsync(record);

            await _handler.HandleAsync(record.Id, "corr-4");

            _mockTranscriptionService.Verify(s => s.TranscribeAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
