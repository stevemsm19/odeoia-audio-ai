using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AudioProcessor.Application.Interfaces;
using AudioProcessor.Domain.Entities;
using AudioProcessor.Domain.Enums;
using AudioProcessor.Infrastructure.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace AudioProcessor.Tests.Infrastructure
{
    public class AudioBackgroundProcessingServiceTests
    {
        private static AudioRecord NewPendingRecord() => new(
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
        /// Sets up a real DI container (mirroring how AudioProcessingLauncherTests exercises
        /// scoping) so the service resolves a fresh scope per poll tick and per claimed audio,
        /// exactly as it does against the real ASP.NET Core host.
        /// </summary>
        private static (AudioBackgroundProcessingService Service, IServiceProvider Provider) BuildService(
            Mock<IAudioRepository> mockRepository,
            Mock<IAudioProcessingLauncher> mockLauncher,
            TimeSpan pollInterval,
            int maxDegreeOfParallelism)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton(mockRepository.Object);
            services.AddSingleton(mockLauncher.Object);
            var provider = services.BuildServiceProvider();

            var service = new AudioBackgroundProcessingService(
                provider.GetRequiredService<IServiceScopeFactory>(),
                provider.GetRequiredService<ILogger<AudioBackgroundProcessingService>>(),
                pollInterval: pollInterval,
                maxDegreeOfParallelism: maxDegreeOfParallelism);

            return (service, provider);
        }

        /// <summary>
        /// Verifies the loop's core admission contract: audios sitting in Pending get
        /// claimed (moved out of Pending so a later tick won't re-claim them) and, once
        /// the launcher runs, reach a terminal per-stage status.
        /// </summary>
        [Fact]
        public async Task ExecuteAsync_ClaimsPendingRecords_AndDrivesThemToATerminalState()
        {
            var record1 = NewPendingRecord();
            var record2 = NewPendingRecord();
            var pending = new List<AudioRecord> { record1, record2 };

            int claimedOnce = 0;
            var mockRepository = new Mock<IAudioRepository>();
            mockRepository
                .Setup(r => r.GetPendingAudioRecordsAsync(It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult<IReadOnlyList<AudioRecord>>(
                    Interlocked.Exchange(ref claimedOnce, 1) == 0 ? pending : Array.Empty<AudioRecord>()));
            mockRepository
                .Setup(r => r.UpdateAudioRecordAsync(It.IsAny<AudioRecord>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var mockLauncher = new Mock<IAudioProcessingLauncher>();
            mockLauncher
                .Setup(l => l.ProcessAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns((Guid id, string _, CancellationToken _) =>
                {
                    var record = pending.Single(r => r.Id == id);
                    record.MarkAsCompleted("https://example.supabase.co/audio-files/compressed.aac", 5);
                    record.MarkTranscriptionAsCompleted(5);
                    record.MarkSummaryAsCompleted("https://example.supabase.co/audio-files/summary.txt", 5);
                    return Task.CompletedTask;
                });

            var (service, provider) = BuildService(mockRepository, mockLauncher, TimeSpan.FromMilliseconds(20), maxDegreeOfParallelism: 4);
            using (provider as IDisposable)
            {
                await service.StartAsync(CancellationToken.None);
                try
                {
                    bool bothReachedTerminalState = WaitUntil(
                        () => record1.OverallStatus == AudioOverallStatus.Completed && record2.OverallStatus == AudioOverallStatus.Completed,
                        TimeSpan.FromSeconds(3));

                    Assert.True(bothReachedTerminalState,
                        $"Expected both claimed audios to reach a terminal state. record1={record1.OverallStatus}, record2={record2.OverallStatus}");
                }
                finally
                {
                    await service.StopAsync(CancellationToken.None);
                }
            }

            // Claiming means every stage moved off Pending before the launcher ever ran —
            // otherwise a second poll tick (which this test lets happen, given the short
            // interval) could pick the same record up again.
            mockRepository.Verify(
                r => r.UpdateAudioRecordAsync(It.Is<AudioRecord>(rec => rec.Id == record1.Id || rec.Id == record2.Id), It.IsAny<CancellationToken>()),
                Times.AtLeast(2));
        }

        /// <summary>
        /// The whole point of this hito's admission-control layer: bound how many whole
        /// audios run at once. Six audios with an artificial delay and MaxDegreeOfParallelism=2
        /// must never have more than 2 running concurrently, and must actually saturate that
        /// limit at some point (proving it's a real ceiling, not a coincidence).
        /// </summary>
        [Fact]
        public async Task ExecuteAsync_NeverRunsMoreThanMaxDegreeOfParallelismAudiosConcurrently()
        {
            const int audioCount = 6;
            const int maxDegreeOfParallelism = 2;
            var processingDelay = TimeSpan.FromMilliseconds(150);

            var pending = Enumerable.Range(0, audioCount).Select(_ => NewPendingRecord()).ToList();

            int claimedOnce = 0;
            var mockRepository = new Mock<IAudioRepository>();
            mockRepository
                .Setup(r => r.GetPendingAudioRecordsAsync(It.IsAny<CancellationToken>()))
                .Returns(() => Task.FromResult<IReadOnlyList<AudioRecord>>(
                    Interlocked.Exchange(ref claimedOnce, 1) == 0 ? pending : Array.Empty<AudioRecord>()));
            mockRepository
                .Setup(r => r.UpdateAudioRecordAsync(It.IsAny<AudioRecord>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            int currentConcurrent = 0;
            int maxObservedConcurrent = 0;

            var mockLauncher = new Mock<IAudioProcessingLauncher>();
            mockLauncher
                .Setup(l => l.ProcessAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Returns(async (Guid id, string _, CancellationToken ct) =>
                {
                    int nowConcurrent = Interlocked.Increment(ref currentConcurrent);
                    InterlockedMax(ref maxObservedConcurrent, nowConcurrent);
                    try
                    {
                        await Task.Delay(processingDelay, ct);
                        var record = pending.Single(r => r.Id == id);
                        record.MarkAsCompleted("https://example.supabase.co/audio-files/compressed.aac", 5);
                        record.MarkTranscriptionAsCompleted(5);
                        record.MarkSummaryAsCompleted("https://example.supabase.co/audio-files/summary.txt", 5);
                    }
                    finally
                    {
                        Interlocked.Decrement(ref currentConcurrent);
                    }
                });

            var (service, provider) = BuildService(mockRepository, mockLauncher, TimeSpan.FromMilliseconds(20), maxDegreeOfParallelism);
            using (provider as IDisposable)
            {
                await service.StartAsync(CancellationToken.None);
                try
                {
                    bool allCompleted = WaitUntil(
                        () => pending.All(r => r.OverallStatus == AudioOverallStatus.Completed),
                        TimeSpan.FromSeconds(5));

                    Assert.True(allCompleted, "Expected every claimed audio to eventually complete.");
                }
                finally
                {
                    await service.StopAsync(CancellationToken.None);
                }
            }

            Assert.True(maxObservedConcurrent <= maxDegreeOfParallelism,
                $"MaxDegreeOfParallelism={maxDegreeOfParallelism} was violated: observed {maxObservedConcurrent} audios processing at once.");
            Assert.Equal(maxDegreeOfParallelism, maxObservedConcurrent);
        }

        private static void InterlockedMax(ref int target, int candidate)
        {
            int initial;
            do
            {
                initial = target;
                if (candidate <= initial)
                    return;
            } while (Interlocked.CompareExchange(ref target, candidate, initial) != initial);
        }
    }
}
