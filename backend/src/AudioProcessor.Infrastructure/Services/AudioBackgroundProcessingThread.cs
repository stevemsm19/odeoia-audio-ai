using System;
using System.Linq;
using System.Threading;
using AudioProcessor.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AudioProcessor.Infrastructure.Services
{
    public class AudioBackgroundProcessingThread
    {
        private const int DefaultPollIntervalSeconds = 3;

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AudioBackgroundProcessingThread> _logger;
        private readonly TimeSpan _pollInterval;

        public AudioBackgroundProcessingThread(
            IServiceScopeFactory scopeFactory,
            ILogger<AudioBackgroundProcessingThread> logger,
            TimeSpan? pollInterval = null)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _pollInterval = pollInterval ?? TimeSpan.FromSeconds(ReadPositiveInt("BACKGROUND_PROCESSING_POLL_INTERVAL_SECONDS", DefaultPollIntervalSeconds));
        }

        public void Start()
        {
            var thread = new Thread(Loop)
            {
                IsBackground = true,
                Name = "AudioBackgroundProcessing"
            };
            thread.Start();
            _logger.LogInformation("AudioBackgroundProcessingThread started (poll interval: {PollInterval}).", _pollInterval);
        }

        private void Loop()
        {
            while (true)
            {
                try
                {
                    Guid? claimedId = TryClaimNextPending();
                    if (claimedId is null)
                    {
                        Thread.Sleep(_pollInterval);
                        continue;
                    }

                    using var scope = _scopeFactory.CreateScope();
                    var launcher = scope.ServiceProvider.GetRequiredService<IAudioProcessingLauncher>();
                    launcher.Process(claimedId.Value, correlationId: $"background-{claimedId.Value}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected failure while polling for pending audios — will retry on the next tick.");
                    Thread.Sleep(_pollInterval);
                }
            }
        }

        private Guid? TryClaimNextPending()
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IAudioRepository>();

            var record = repository.GetPendingAudioRecords().FirstOrDefault();
            if (record is null)
                return null;

            record.MarkAsProcessing();
            record.MarkTranscriptionAsProcessing();
            record.MarkSummaryAsProcessing();
            repository.UpdateAudioRecord(record);

            _logger.LogInformation("Claimed pending audio {Id} for background processing.", record.Id);
            return record.Id;
        }

        private static int ReadPositiveInt(string environmentVariableName, int defaultValue)
        {
            string? raw = Environment.GetEnvironmentVariable(environmentVariableName);
            int value = int.TryParse(raw, out var parsed) ? parsed : defaultValue;
            return Math.Max(1, value);
        }
    }
}
