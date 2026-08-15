using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using AudioProcessor.Application.Interfaces;
using AudioProcessor.Application.Logging;
using AudioProcessor.Application.UseCases;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AudioProcessor.Infrastructure.Services
{
    /// <summary>
    /// Downloads the original audio once, then starts the two flows (Compression;
    /// Transcription→Summary) as independent <see cref="Task"/>s via <c>Task.Run</c>,
    /// each with its own DI scope (EF Core's <c>DbContext</c> is not thread-safe to
    /// share). Returns immediately — the flows run and update their own state
    /// independently.
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
            _ = RunBothFlowsAsync(audioRecordId, correlationId);
        }

        private async Task RunBothFlowsAsync(Guid audioRecordId, string correlationId)
        {
            try
            {
                byte[]? originalAudioBytes = await DownloadOriginalAudioAsync(audioRecordId, correlationId);
                if (originalAudioBytes is null)
                    return;

                Task compressionTask = Task.Run(() => RunFlowAsync<ProcessAudioCompressionCommandHandler>(
                    "Compression", audioRecordId, correlationId, originalAudioBytes,
                    (handler, id, corrId, stream) => handler.HandleAsync(id, stream, corrId)));

                Task summaryTask = Task.Run(() => RunFlowAsync<ProcessAudioSummaryCommandHandler>(
                    "Transcription+Summary", audioRecordId, correlationId, originalAudioBytes,
                    (handler, id, corrId, stream) => handler.HandleAsync(id, stream, corrId)));

                await Task.WhenAll(compressionTask, summaryTask);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Unexpected unhandled exception while orchestrating processing for record {Id} — this should never happen.",
                    audioRecordId);
            }
        }

        /// <summary>
        /// Downloads the original audio a single time so both flows can share it in
        /// memory instead of each independently re-downloading it from Storage.
        /// </summary>
        /// <returns>The audio bytes, or <c>null</c> if the record wasn't found or the
        /// download failed (in which case every stage is already persisted as Failed).</returns>
        private async Task<byte[]?> DownloadOriginalAudioAsync(Guid audioRecordId, string correlationId)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IAudioRepository>();
                var httpClient = scope.ServiceProvider.GetRequiredService<HttpClient>();

                var record = await repository.GetByIdAsync(audioRecordId);
                if (record is null)
                {
                    _logger.LogError("Audio record with ID {Id} was not found in the database. Aborting processing.", audioRecordId);
                    return null;
                }

                using var response = await httpClient.GetAsync(record.StorageUrl, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();
                byte[] bytes = await response.Content.ReadAsByteArrayAsync();

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
            Func<THandler, Guid, string, Stream, Task> invoke)
            where THandler : notnull
        {
            // Must wrap the entire flow body: an uncaught exception here would only
            // ever come from DI/scope setup, since every handler already contains
            // and persists its own failures.
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService<THandler>();
                using var originalStream = new MemoryStream(originalAudioBytes, writable: false);
                await invoke(handler, audioRecordId, correlationId, originalStream);
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
