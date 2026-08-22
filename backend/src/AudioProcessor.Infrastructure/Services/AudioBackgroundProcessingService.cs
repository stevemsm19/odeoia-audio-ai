using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AudioProcessor.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AudioProcessor.Infrastructure.Services
{
    /// <summary>
    /// Polls for uploaded audios still in <c>Pending</c>, claims a batch by marking
    /// them <c>Processing</c>, then runs the batch through <see cref="IAudioProcessingLauncher"/>
    /// with <c>Parallel.ForEachAsync</c>. Its <c>MaxDegreeOfParallelism</c> is the
    /// system-level admission control on how many *whole audios* run at once — a
    /// different layer from the global <c>SemaphoreSlim</c> that limits concurrent
    /// heavy external processes inside a single flow, and from the <c>Task.WhenAll</c>
    /// that runs the two flows of one audio concurrently.
    /// </summary>
    public class AudioBackgroundProcessingService : BackgroundService
    {
        private const int DefaultPollIntervalSeconds = 3;
        private const int DefaultMaxDegreeOfParallelism = 4;

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AudioBackgroundProcessingService> _logger;
        private readonly TimeSpan _pollInterval;
        private readonly int _maxDegreeOfParallelism;

        /// <param name="pollInterval">Overrides <c>BACKGROUND_PROCESSING_POLL_INTERVAL_SECONDS</c> — for tests only, production wiring leaves this null.</param>
        /// <param name="maxDegreeOfParallelism">Overrides <c>BACKGROUND_PROCESSING_MAX_DEGREE_OF_PARALLELISM</c> — for tests only, production wiring leaves this null.</param>
        public AudioBackgroundProcessingService(
            IServiceScopeFactory scopeFactory,
            ILogger<AudioBackgroundProcessingService> logger,
            TimeSpan? pollInterval = null,
            int? maxDegreeOfParallelism = null)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _pollInterval = pollInterval ?? TimeSpan.FromSeconds(ReadPositiveInt("BACKGROUND_PROCESSING_POLL_INTERVAL_SECONDS", DefaultPollIntervalSeconds));
            _maxDegreeOfParallelism = maxDegreeOfParallelism ?? ReadPositiveInt("BACKGROUND_PROCESSING_MAX_DEGREE_OF_PARALLELISM", DefaultMaxDegreeOfParallelism);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(_pollInterval);
            do
            {
                try
                {
                    await ProcessPendingBatchAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // A transient failure here (e.g. the database is briefly unreachable)
                    // must not kill the loop for the rest of the process's lifetime.
                    _logger.LogError(ex, "Unexpected failure while polling for pending audios — will retry on the next tick.");
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
        }

        private async Task ProcessPendingBatchAsync(CancellationToken stoppingToken)
        {
            List<Guid> claimedIds = new();

            using (var scope = _scopeFactory.CreateScope())
            {
                var repository = scope.ServiceProvider.GetRequiredService<IAudioRepository>();
                var pending = await repository.GetPendingAudioRecordsAsync(stoppingToken);
                if (pending.Count == 0)
                    return;

                foreach (var record in pending)
                {
                    record.MarkAsProcessing();
                    record.MarkTranscriptionAsProcessing();
                    record.MarkSummaryAsProcessing();
                    await repository.UpdateAudioRecordAsync(record, stoppingToken);
                    claimedIds.Add(record.Id);
                }
            }

            _logger.LogInformation("Claimed {Count} pending audio(s) for background processing (max degree of parallelism: {MaxDop}).",
                claimedIds.Count, _maxDegreeOfParallelism);

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = _maxDegreeOfParallelism,
                CancellationToken = stoppingToken,
            };

            await Parallel.ForEachAsync(claimedIds, parallelOptions, async (audioId, ct) =>
            {
                // Each audio gets its own DI scope so it resolves its own launcher/handlers
                // instead of sharing state across concurrent Parallel.ForEachAsync bodies.
                using var scope = _scopeFactory.CreateScope();
                var launcher = scope.ServiceProvider.GetRequiredService<IAudioProcessingLauncher>();
                await launcher.ProcessAsync(audioId, correlationId: $"background-{audioId}", ct);
            });
        }

        private static int ReadPositiveInt(string environmentVariableName, int defaultValue)
        {
            string? raw = Environment.GetEnvironmentVariable(environmentVariableName);
            int value = int.TryParse(raw, out var parsed) ? parsed : defaultValue;
            return Math.Max(1, value);
        }
    }
}
