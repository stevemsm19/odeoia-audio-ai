using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AudioProcessor.Application.Interfaces;
using AudioProcessor.Application.Logging;
using AudioProcessor.Application.UseCases;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AudioProcessor.Infrastructure.Services
{
    // Runs Compression and Transcription→Summary as independent tasks (TPL), each in its own DI scope.
    public class AudioProcessingLauncher : IAudioProcessingLauncher
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AudioProcessingLauncher> _logger;

        public AudioProcessingLauncher(IServiceScopeFactory scopeFactory, ILogger<AudioProcessingLauncher> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task ProcessAsync(Guid audioRecordId, string correlationId, CancellationToken cancellationToken = default)
        {
            try
            {
                byte[]? originalAudioBytes = await DownloadOriginalAudioAsync(audioRecordId, correlationId, cancellationToken);
                if (originalAudioBytes is null)
                    return;

                // Concurrent flows: compression and transcription+summary don't depend on each other.
                Task compressionTask = Task.Run(() => RunFlowAsync<ProcessAudioCompressionCommandHandler>(
                    "Compression", audioRecordId, correlationId, originalAudioBytes,
                    (handler, id, corrId, stream, ct) => handler.HandleAsync(id, stream, corrId, ct),
                    cancellationToken));

                Task summaryTask = Task.Run(() => RunFlowAsync<ProcessAudioSummaryCommandHandler>(
                    "Transcription+Summary", audioRecordId, correlationId, originalAudioBytes,
                    (handler, id, corrId, stream, ct) => handler.HandleAsync(id, stream, corrId, ct),
                    cancellationToken));

                await Task.WhenAll(compressionTask, summaryTask);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Unexpected unhandled exception while orchestrating processing for record {Id} — this should never happen.",
                    audioRecordId);
            }
        }

        // Downloads the audio once so both flows share it in memory instead of re-fetching from Storage.
        private async Task<byte[]?> DownloadOriginalAudioAsync(Guid audioRecordId, string correlationId, CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IAudioRepository>();
                var httpClient = scope.ServiceProvider.GetRequiredService<HttpClient>();

                var record = await repository.GetByIdAsync(audioRecordId, cancellationToken);
                if (record is null)
                {
                    _logger.LogError("Audio record with ID {Id} was not found in the database. Aborting processing.", audioRecordId);
                    return null;
                }

                using var response = await httpClient.GetAsync(record.StorageUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();
                byte[] bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);

                AudioPerformanceLog.Write(_logger, audioRecordId, "Shared", correlationId, "AudioDownload", "Completed", stopwatch.ElapsedMilliseconds);
                return bytes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to download the original audio for record {Id} — marking every stage as Failed.", audioRecordId);
                AudioPerformanceLog.Write(_logger, audioRecordId, "Shared", correlationId, "AudioDownload", "Failed", stopwatch.ElapsedMilliseconds);
                await MarkAllStagesFailedAsync(audioRecordId, stopwatch.ElapsedMilliseconds);
                return null;
            }
        }

        private async Task MarkAllStagesFailedAsync(Guid audioRecordId, long elapsedMilliseconds)
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IAudioRepository>();

            var record = await repository.GetByIdAsync(audioRecordId);
            if (record is null)
                return;

            record.MarkAsFailed(elapsedMilliseconds);
            record.MarkTranscriptionAsFailed("The original audio could not be downloaded.", elapsedMilliseconds);
            record.MarkSummaryAsFailed("Summary was not attempted because the original audio could not be downloaded.", 0);
            await repository.UpdateAudioRecordAsync(record);
        }

        private async Task RunFlowAsync<THandler>(
            string flowName,
            Guid audioRecordId,
            string correlationId,
            byte[] originalAudioBytes,
            Func<THandler, Guid, string, Stream, CancellationToken, Task> invoke,
            CancellationToken cancellationToken)
            where THandler : notnull
        {
            // Only DI/scope failures land here — handlers persist their own errors.
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService<THandler>();
                using var originalStream = new MemoryStream(originalAudioBytes, writable: false);
                await invoke(handler, audioRecordId, correlationId, originalStream, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Unexpected unhandled exception in {Flow} flow for record {Id} — this should never happen, every internal step already has its own try/catch.",
                    flowName, audioRecordId);
            }
        }
    }
}
