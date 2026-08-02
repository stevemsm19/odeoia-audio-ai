using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using AudioProcessor.Application.Interfaces;
using AudioProcessor.Application.UseCases;
using AudioProcessor.Application.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AudioProcessor.Api.BackgroundServices
{
    /// <summary>
    /// Hosted background service that processes audio compression jobs asynchronously
    /// from the in-memory <see cref="IBackgroundJobQueue"/>.
    /// Runs for the lifetime of the application host.
    /// </summary>
    public class AudioProcessingBackgroundService : BackgroundService
    {
        private readonly IBackgroundJobQueue _queue;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<AudioProcessingBackgroundService> _logger;

        public AudioProcessingBackgroundService(
            IBackgroundJobQueue queue,
            IServiceProvider serviceProvider,
            ILogger<AudioProcessingBackgroundService> logger)
        {
            _queue = queue ?? throw new ArgumentNullException(nameof(queue));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Audio Processing Background Service is starting.");

            int workerCount = Math.Max(2, Math.Min(Environment.ProcessorCount, 4));
            Task[] workers = new Task[workerCount];
            for (int i = 0; i < workerCount; i++)
            {
                workers[i] = ConsumeAsync(i + 1, stoppingToken);
            }
            await Task.WhenAll(workers);
            _logger.LogInformation("Audio Processing Background Service has stopped.");
        }

        private async Task ConsumeAsync(int workerNumber, CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    AudioProcessingJob job = await _queue.DequeueJobAsync(stoppingToken);

                    AudioPerformanceLog.Write(_logger, job.AudioRecordId, job.Type, job.CorrelationId, "QueueWait", "Completed", Math.Max(0, (long)(DateTimeOffset.UtcNow - job.EnqueuedAtUtc).TotalMilliseconds));

                    _logger.LogInformation("Worker {Worker} dequeued {JobType} job for record {Id}", workerNumber, job.Type, job.AudioRecordId);
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var stopwatch = Stopwatch.StartNew();

                        try
                        {
                            if (job.Type == AudioProcessingJobType.Compression)
                            {
                                var handler = scope.ServiceProvider.GetRequiredService<ProcessAudioCompressionCommandHandler>();
                                await handler.HandleAsync(job.AudioRecordId, job.CorrelationId, stoppingToken);
                            }
                            else
                            {
                                var handler = scope.ServiceProvider.GetRequiredService<ProcessAudioSummaryCommandHandler>();
                                await handler.HandleAsync(job.AudioRecordId, job.CorrelationId, stoppingToken);
                            }

                            stopwatch.Stop();
                            _logger.LogInformation("Background {JobType} workflow completed for record {Id}. Duration: {DurationMs} ms",
                                job.Type, job.AudioRecordId, stopwatch.ElapsedMilliseconds);
                            AudioPerformanceLog.Write(_logger, job.AudioRecordId, job.Type, job.CorrelationId, "EndToEnd", "Completed", stopwatch.ElapsedMilliseconds);
                        }
                        catch (Exception ex)
                        {
                            stopwatch.Stop();
                            _logger.LogError(ex, "Background {JobType} workflow failed for record {Id}. Terminated after: {DurationMs} ms",
                                job.Type, job.AudioRecordId, stopwatch.ElapsedMilliseconds);
                            AudioPerformanceLog.Write(_logger, job.AudioRecordId, job.Type, job.CorrelationId, "EndToEnd", "Failed", stopwatch.ElapsedMilliseconds);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Audio processing background queue consumer received cancellation. Stopping worker.");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An unhandled error occurred in the background queue dequeue loop.");
                }
            }

        }
    }
}
