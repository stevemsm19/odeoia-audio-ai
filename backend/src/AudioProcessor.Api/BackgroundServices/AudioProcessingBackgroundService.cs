using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using AudioProcessor.Application.Interfaces;
using AudioProcessor.Application.UseCases;
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

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    Guid audioRecordId = await _queue.DequeueJobAsync(stoppingToken);

                    _logger.LogInformation("Dequeued audio compression job for Record ID: {Id}", audioRecordId);
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var handler = scope.ServiceProvider.GetRequiredService<ProcessAudioCompressionCommandHandler>();

                        var stopwatch = Stopwatch.StartNew();
                        _logger.LogInformation("Initiating background transcoder workflow for Record ID: {Id}", audioRecordId);

                        try
                        {
                            await handler.HandleAsync(audioRecordId, stoppingToken);

                            stopwatch.Stop();
                            _logger.LogInformation("Background compression workflow completed successfully for Record ID: {Id}. Duration: {DurationMs} ms",
                                audioRecordId, stopwatch.ElapsedMilliseconds);
                        }
                        catch (Exception ex)
                        {
                            stopwatch.Stop();
                            _logger.LogError(ex, "Background compression workflow failed for Record ID: {Id}. Terminated after: {DurationMs} ms",
                                audioRecordId, stopwatch.ElapsedMilliseconds);
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

            _logger.LogInformation("Audio Processing Background Service has stopped.");
        }
    }
}
