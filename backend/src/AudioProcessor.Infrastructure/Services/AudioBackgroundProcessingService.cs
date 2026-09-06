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
    /// <summary>
    /// Polls for uploaded audios still in <c>Pending</c>, claims them by marking them
    /// <c>Processing</c>, and dispatches each onto a fixed-size pool of
    /// <c>MaxDegreeOfParallelism</c> slots (a <see cref="SemaphoreSlim"/>). As soon as any
    /// slot frees up — regardless of which poll tick claimed the audio occupying it — the
    /// next claimed-but-not-yet-started audio takes it immediately. This makes the sliding
    /// window span the whole claimed backlog, not just the audios claimed in a single poll:
    /// a new poll's claims join the same pool as any audios still running from a previous
    /// poll, instead of waiting for that previous poll's entire claim to drain first.
    /// <c>MaxDegreeOfParallelism</c> is the system-level admission control on how many
    /// *whole audios* run at once — a different layer from the global <c>SemaphoreSlim</c>
    /// that limits concurrent heavy external processes inside a single flow, and from the
    /// <c>Task.WhenAll</c> that runs the two flows of one audio concurrently.
    /// </summary>
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
                        // A transient failure here (e.g. the database is briefly unreachable)
                        // must not kill the loop for the rest of the process's lifetime.
                        _logger.LogError(ex, "Unexpected failure while polling for pending audios — will retry on the next tick.");
                    }
                } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
            }
            finally
            {
                // The loop above only dispatches work — it never waits for it to finish, so
                // audios claimed by the last poll (or an earlier one that hadn't drained yet)
                // could still be running here. Wait for them so the host doesn't tear down the
                // DI container out from under a task that's mid-flight.
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

            foreach (var audioId in claimedIds)
            {
                // Blocks only when every slot is already occupied by audios still running —
                // possibly claimed by an earlier poll tick. As soon as any one of them
                // finishes, this (or the next waiting) audio takes the freed slot immediately.
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
                // Each audio gets its own DI scope so it resolves its own launcher/handlers
                // instead of sharing state across concurrently dispatched audios.
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
