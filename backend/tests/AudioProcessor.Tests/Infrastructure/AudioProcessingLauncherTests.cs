using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AudioProcessor.Application.Interfaces;
using AudioProcessor.Application.UseCases;
using AudioProcessor.Domain.Entities;
using AudioProcessor.Domain.Enums;
using AudioProcessor.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Xunit;

namespace AudioProcessor.Tests.Infrastructure
{
    public class AudioProcessingLauncherTests
    {
        private static AudioRecord NewRecord() => new(
            id: Guid.NewGuid(),
            storageUrl: "https://example.supabase.co/audio-files/original.mp3",
            fileName: "original.mp3",
            createdAt: DateTime.UtcNow);

        private static bool WaitUntil(Func<bool> condition, TimeSpan timeout)
        {
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (condition())
                    return true;
                Thread.Sleep(20);
            }
            return condition();
        }

        /// <summary>
        /// An exception outside every handler's own try/catch — here, simulated by
        /// DI scope creation failing before the shared download can even run — must
        /// be contained and logged, not left to crash the process or vanish silently.
        /// With Task instead of a raw Thread, an uncaught exception can no longer
        /// crash the process on its own; what still matters is that it's actually
        /// observed (logged) rather than swallowed as an unobserved Task exception.
        /// </summary>
        [Fact]
        public async Task ProcessAsync_WhenScopeCreationThrows_DoesNotThrow_AndLogsTheFailure()
        {
            var mockScopeFactory = new Mock<IServiceScopeFactory>();
            mockScopeFactory
                .Setup(f => f.CreateScope())
                .Throws(new InvalidOperationException("Simulated failure before any handler ever runs."));

            var mockLogger = new Mock<ILogger<AudioProcessingLauncher>>();
            var launcher = new AudioProcessingLauncher(mockScopeFactory.Object, mockLogger.Object);

            var audioRecordId = Guid.NewGuid();

            // Every failure inside processing must be contained and logged, never
            // rethrown to the caller (in production, the background processing loop).
            var exception = await Record.ExceptionAsync(() => launcher.ProcessAsync(audioRecordId, "corr-r8"));
            Assert.Null(exception);

            bool loggedTheFailure = WaitUntil(() => CountErrorLogs(mockLogger) >= 1, TimeSpan.FromSeconds(2));
            Assert.True(loggedTheFailure, "Expected the contained exception to be logged via ILogger instead of being silently lost.");
        }

        /// <summary>
        /// End-to-end through a real DI container (not mocked IServiceScope chains):
        /// verifies the two flows actually run as independent Tasks and reach
        /// Completed, and that the original audio is downloaded exactly once and
        /// shared between them rather than each flow re-downloading it.
        /// </summary>
        [Fact]
        public async Task ProcessAsync_DownloadsOriginalAudioOnce_AndCompletesBothFlowsConcurrently()
        {
            var record = NewRecord();

            var mockAudioRepository = new Mock<IAudioRepository>();
            mockAudioRepository.Setup(r => r.GetByIdAsync(record.Id, It.IsAny<CancellationToken>())).ReturnsAsync(record);
            mockAudioRepository.Setup(r => r.UpdateAudioRecordAsync(It.IsAny<AudioRecord>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

            var mockHttpMessageHandler = new Mock<HttpMessageHandler>();
            mockHttpMessageHandler
                .Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() => new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StreamContent(new MemoryStream(new byte[] { 1, 2, 3, 4, 5 }))
                });

            var mockCompressionService = new Mock<IAudioCompressionService>();
            mockCompressionService
                .Setup(c => c.CompressToAacAsync(It.IsAny<Stream>(), record.FileName, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MemoryStream(new byte[] { 9, 9, 9 }));

            var mockNoiseReductionService = new Mock<IAudioNoiseReductionService>();
            mockNoiseReductionService
                .Setup(n => n.ReduceNoiseAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MemoryStream(new byte[] { 7, 7, 7 }));

            var mockTranscriptionService = new Mock<IAudioTranscriptionService>();
            mockTranscriptionService
                .Setup(s => s.TranscribeAsync(It.IsAny<Stream>(), record.FileName, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AudioTranscriptionResult("hello world", 10));

            var mockSummarizationService = new Mock<IAudioSummarizationService>();
            mockSummarizationService
                .Setup(s => s.SummarizeAsync("hello world", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AudioSummarizationResult("a short summary", 10));

            var mockStorageService = new Mock<IAudioStorageService>();
            mockStorageService
                .Setup(s => s.UploadAudioAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), record.Id, It.IsAny<CancellationToken>()))
                .ReturnsAsync("https://example.supabase.co/audio-files/result");

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton(mockAudioRepository.Object);
            services.AddSingleton(new HttpClient(mockHttpMessageHandler.Object));
            services.AddSingleton(mockCompressionService.Object);
            services.AddSingleton(mockNoiseReductionService.Object);
            services.AddSingleton(mockTranscriptionService.Object);
            services.AddSingleton(mockSummarizationService.Object);
            services.AddSingleton(mockStorageService.Object);
            services.AddScoped<ProcessAudioCompressionCommandHandler>();
            services.AddScoped<ProcessAudioSummaryCommandHandler>();

            using var provider = services.BuildServiceProvider();
            var launcher = new AudioProcessingLauncher(
                provider.GetRequiredService<IServiceScopeFactory>(),
                provider.GetRequiredService<ILogger<AudioProcessingLauncher>>());

            await launcher.ProcessAsync(record.Id, "corr-shared-download");

            Assert.True(
                record.Status == AudioProcessingStatus.Completed && record.SummaryStatus == AudioSummaryStatus.Completed,
                $"Expected both flows to complete. Compression={record.Status}, Summary={record.SummaryStatus}");

            mockHttpMessageHandler
                .Protected()
                .Verify("SendAsync", Times.Once(), ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>());
        }

        private static int CountErrorLogs(Mock<ILogger<AudioProcessingLauncher>> mockLogger)
        {
            return mockLogger.Invocations.Count(invocation =>
                invocation.Method.Name == nameof(ILogger.Log)
                && invocation.Arguments.Count > 0
                && invocation.Arguments[0] is LogLevel.Error);
        }
    }
}
