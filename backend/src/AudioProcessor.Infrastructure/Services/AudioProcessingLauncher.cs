using System;
using System.Threading;
using System.Threading.Tasks;
using AudioProcessor.Application.Interfaces;
using AudioProcessor.Application.UseCases;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AudioProcessor.Infrastructure.Services
{
    /// <summary>
    /// Starts an explicit <see cref="Thread"/> per flow, per audio — no <c>Task.Run</c>,
    /// no queue, no shared worker pool. Each Thread creates its own DI scope
    /// (EF Core's <c>DbContext</c> is not thread-safe to share) and bridges into
    /// the existing async command handlers via a single blocking
    /// <c>.GetAwaiter().GetResult()</c> call.
    /// </summary>
    public class AudioProcessingLauncher : IAudioProcessingLauncher
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AudioProcessingLauncher> _logger;

        public AudioProcessingLauncher(IServiceScopeFactory scopeFactory, ILogger<AudioProcessingLauncher> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public void LaunchProcessing(Guid audioRecordId, string correlationId)
        {
            StartFlowThread<ProcessAudioCompressionCommandHandler>(
                "Compression", audioRecordId, correlationId,
                (handler, id, corrId) => handler.HandleAsync(id, corrId));

            StartFlowThread<ProcessAudioSummaryCommandHandler>(
                "Transcription+Summary", audioRecordId, correlationId,
                (handler, id, corrId) => handler.HandleAsync(id, corrId));
        }

        private void StartFlowThread<THandler>(
            string flowName,
            Guid audioRecordId,
            string correlationId,
            Func<THandler, Guid, string, Task> invoke)
            where THandler : notnull
        {
            var thread = new Thread(() =>
            {
                // Must wrap the entire thread body: an uncaught exception on a plain
                // Thread terminates the whole ASP.NET Core process, not just this flow.
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var handler = scope.ServiceProvider.GetRequiredService<THandler>();
                    invoke(handler, audioRecordId, correlationId).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Unexpected unhandled exception in {Flow} thread for record {Id} — this should never happen, every internal step already has its own try/catch.",
                        flowName, audioRecordId);
                }
            })
            {
                IsBackground = true,
                Name = $"AudioFlow-{flowName}-{audioRecordId:N}"
            };

            thread.Start();
        }
    }
}
