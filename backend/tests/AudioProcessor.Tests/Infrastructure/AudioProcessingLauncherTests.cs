using System;
using System.Threading;
using AudioProcessor.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AudioProcessor.Tests.Infrastructure
{
    /// <summary>
    /// An exception outside a handler's own try/catch — for example, during DI
    /// scope creation — must be contained by AudioProcessingLauncher's outer
    /// try/catch, not crash the process.
    /// </summary>
    public class AudioProcessingLauncherTests
    {
        [Fact]
        public void LaunchProcessing_WhenScopeCreationThrows_DoesNotThrowSynchronously_AndLogsTheFailure()
        {
            var mockScopeFactory = new Mock<IServiceScopeFactory>();
            mockScopeFactory
                .Setup(f => f.CreateScope())
                .Throws(new InvalidOperationException("Simulated failure before the handler's own try/catch ever runs."));

            var mockLogger = new Mock<ILogger<AudioProcessingLauncher>>();
            var launcher = new AudioProcessingLauncher(mockScopeFactory.Object, mockLogger.Object);

            var audioRecordId = Guid.NewGuid();

            // Must return immediately without throwing — the Threads it starts run
            // independently, and any failure inside them must never reach the caller
            // (which, in production, is an HTTP request thread).
            var exception = Record.Exception(() => launcher.LaunchProcessing(audioRecordId, "corr-r8"));
            Assert.Null(exception);

            // The background Threads run asynchronously; poll briefly for both of
            // them (Compression and Transcription+Summary) to have logged the error
            // instead of crashing the process.
            bool bothFlowsLogged = WaitUntil(() => CountErrorLogs(mockLogger) >= 2, TimeSpan.FromSeconds(2));
            Assert.True(bothFlowsLogged, "Expected both flow Threads to log the contained exception via ILogger.");
        }

        private static int CountErrorLogs(Mock<ILogger<AudioProcessingLauncher>> mockLogger)
        {
            return mockLogger.Invocations.Count(invocation =>
                invocation.Method.Name == nameof(ILogger.Log)
                && invocation.Arguments.Count > 0
                && invocation.Arguments[0] is LogLevel.Error);
        }

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
    }
}
