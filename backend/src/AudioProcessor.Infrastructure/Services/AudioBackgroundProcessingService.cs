using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AudioProcessor.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AudioProcessor.Infrastructure.Services
{
    // Polls for Pending audios and admits up to MaxDegreeOfParallelism at once through a
    // SemaphoreSlim slot pool — a sliding window over the whole backlog, not a per-poll batch.
    public class AudioBackgroundProcessingService : BackgroundService
    {
        private const int DefaultPollIntervalSeconds = 3;
        private const int DefaultMaxDegreeOfParallelism = 4;

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AudioBackgroundProcessingService> _logger;
        private readonly TimeSpan _pollInterval;
        private readonly int _maxDegreeOfParallelism;
        private readonly SemaphoreSlim _slots;
        private readonly ConcurrentDictionary<Task, byte> _inFlight = new();

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
            _slots = new SemaphoreSlim(_maxDegreeOfParallelism, _maxDegreeOfParallelism);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(_pollInterval);
            try
            {
                do
                {
                    try
                    {
                        await ClaimAndDispatchPendingAsync(stoppingToken);
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        // Transient failures (e.g. DB briefly unreachable) must not kill the polling loop.
                        _logger.LogError(ex, "Unexpected failure while polling for pending audios — will retry on the next tick.");
                    }
                } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
            }
            finally
            {
                // Wait for still-running audios before the host tears down the DI container.
                await DrainInFlightAsync().ConfigureAwait(false);
            }
        }

        private async Task ClaimAndDispatchPendingAsync(CancellationToken stoppingToken)
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
                    await repository.UpdateAudioRecordAsync(record, stoppingToken);
                    claimedIds.Add(record.Id);
                }
            }

            _logger.LogInformation("Claimed {Count} pending audio(s) for background processing (max degree of parallelism: {MaxDop}).",
                claimedIds.Count, _maxDegreeOfParallelism);

            // Semaphore admission control: blocks only when all slots are occupied.
            foreach (var audioId in claimedIds)
            {
                await _slots.WaitAsync(stoppingToken).ConfigureAwait(false);
                Dispatch(audioId, stoppingToken);
            }
        }

        private void Dispatch(Guid audioId, CancellationToken stoppingToken)
        {
            var task = ProcessOneAsync(audioId, stoppingToken);
            _inFlight[task] = 0;
            task.ContinueWith(completed =>
            {
                _inFlight.TryRemove(completed, out _);
                _slots.Release();
            }, TaskScheduler.Default);
        }

        private async Task ProcessOneAsync(Guid audioId, CancellationToken stoppingToken)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var launcher = scope.ServiceProvider.GetRequiredService<IAudioProcessingLauncher>();
                await launcher.ProcessAsync(audioId, correlationId: $"background-{audioId}", stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Unexpected failure while processing audio {AudioId} in background.", audioId);
            }
        }

        private async Task DrainInFlightAsync()
        {
            var snapshot = _inFlight.Keys.ToArray();
            if (snapshot.Length == 0)
                return;

            await Task.WhenAll(snapshot).ConfigureAwait(false);
        }

        private static int ReadPositiveInt(string environmentVariableName, int defaultValue)
        {
            string? raw = Environment.GetEnvironmentVariable(environmentVariableName);
            int value = int.TryParse(raw, out var parsed) ? parsed : defaultValue;
            return Math.Max(1, value);
        }
    }
}
