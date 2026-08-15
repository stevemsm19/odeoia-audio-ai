using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace AudioProcessor.Tests.Infrastructure
{
    /// <summary>
    /// Exercises the exact primitive shared by FFmpegAudioCompressionService,
    /// LocalWhisperCppTranscriptionService and LocalLlamaCppSummarizationService:
    /// a single global SemaphoreSlim limiting how many heavy external processes
    /// run at once, across flows.
    /// </summary>
    public class ExternalProcessSemaphoreTests
    {
        [Fact]
        public async Task Semaphore_WithLimitOne_SerializesTheSecondCaller()
        {
            using var semaphore = new SemaphoreSlim(1);
            var firstHolderReady = new ManualResetEventSlim(false);
            var releaseFirstHolder = new ManualResetEventSlim(false);
            var secondCallerAcquired = new ManualResetEventSlim(false);

            var firstHolder = Task.Run(async () =>
            {
                await semaphore.WaitAsync();
                firstHolderReady.Set();
                releaseFirstHolder.Wait();
                semaphore.Release();
            });

            Assert.True(firstHolderReady.Wait(TimeSpan.FromSeconds(5)), "First holder never acquired the semaphore.");

            var secondCaller = Task.Run(async () =>
            {
                await semaphore.WaitAsync();
                secondCallerAcquired.Set();
                semaphore.Release();
            });

            // The second caller must NOT be able to acquire the slot while the first
            // one still holds it — this is the whole point of the shared limit.
            Assert.False(secondCallerAcquired.Wait(TimeSpan.FromMilliseconds(200)),
                "Second caller acquired the semaphore while the first holder still had it — the limit is not being enforced.");

            releaseFirstHolder.Set();

            Assert.True(secondCallerAcquired.Wait(TimeSpan.FromSeconds(5)),
                "Second caller never acquired the semaphore after it was released.");

            await Task.WhenAll(firstHolder, secondCaller);
        }

        [Fact]
        public async Task Semaphore_WithLimitTwo_AllowsTwoConcurrentHoldersButNotAThird()
        {
            using var semaphore = new SemaphoreSlim(2);

            await semaphore.WaitAsync();
            await semaphore.WaitAsync();

            var thirdAcquired = new ManualResetEventSlim(false);
            var thirdCaller = Task.Run(async () =>
            {
                await semaphore.WaitAsync();
                thirdAcquired.Set();
                semaphore.Release();
            });

            Assert.False(thirdAcquired.Wait(TimeSpan.FromMilliseconds(200)),
                "A third caller acquired the semaphore while two slots (the configured limit) were already held.");

            semaphore.Release();

            Assert.True(thirdAcquired.Wait(TimeSpan.FromSeconds(5)));
            await thirdCaller;

            semaphore.Release();
        }
    }
}
