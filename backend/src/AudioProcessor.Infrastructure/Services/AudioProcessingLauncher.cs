using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using AudioProcessor.Application.Interfaces;
using AudioProcessor.Application.Logging;
using AudioProcessor.Application.UseCases;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AudioProcessor.Infrastructure.Services
{
    public class AudioProcessingLauncher : IAudioProcessingLauncher
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AudioProcessingLauncher> _logger;

        public AudioProcessingLauncher(IServiceScopeFactory scopeFactory, ILogger<AudioProcessingLauncher> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public void Process(Guid audioRecordId, string correlationId)
        {
            var totalStopwatch = Stopwatch.StartNew();
            string totalStatus = "Failed";
            try
            {
                byte[]? originalAudioBytes = DownloadOriginalAudio(audioRecordId, correlationId);
                if (originalAudioBytes is null)
                    return;

                var compressionThread = new Thread(() => RunFlow<ProcessAudioCompressionCommandHandler>(
                    "Compression", audioRecordId, correlationId, originalAudioBytes,
                    (handler, id, corrId, stream) => handler.Handle(id, stream, corrId)));
                compressionThread.Start();
                compressionThread.Join();

                var summaryThread = new Thread(() => RunFlow<ProcessAudioSummaryCommandHandler>(
                    "Transcription+Summary", audioRecordId, correlationId, originalAudioBytes,
                    (handler, id, corrId, stream) => handler.Handle(id, stream, corrId)));
                summaryThread.Start();
                summaryThread.Join();

                totalStatus = "Completed";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Unexpected unhandled exception while orchestrating processing for record {Id} — this should never happen.",
                    audioRecordId);
            }
            finally
            {
                AudioPerformanceLog.Write(_logger, audioRecordId, "Shared", correlationId, "TotalProcessing", totalStatus, totalStopwatch.ElapsedMilliseconds);
            }
        }

        private byte[]? DownloadOriginalAudio(Guid audioRecordId, string correlationId)
        {
            var stopwatch = Stopwatch.StartNew();
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IAudioRepository>();
                var httpClient = scope.ServiceProvider.GetRequiredService<HttpClient>();

                var record = repository.GetById(audioRecordId);
                if (record is null)
                {
                    _logger.LogError("Audio record with ID {Id} was not found in the database. Aborting processing.", audioRecordId);
                    return null;
                }

                using var request = new HttpRequestMessage(HttpMethod.Get, record.StorageUrl);
                using var response = httpClient.Send(request, HttpCompletionOption.ResponseHeadersRead);
                response.EnsureSuccessStatusCode();

                using var memoryStream = new MemoryStream();
                using (var contentStream = response.Content.ReadAsStream())
                {
                    contentStream.CopyTo(memoryStream);
                }
                byte[] bytes = memoryStream.ToArray();

                AudioPerformanceLog.Write(_logger, audioRecordId, "Shared", correlationId, "AudioDownload", "Completed", stopwatch.ElapsedMilliseconds);
                return bytes;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to download the original audio for record {Id} — marking every stage as Failed.", audioRecordId);
                AudioPerformanceLog.Write(_logger, audioRecordId, "Shared", correlationId, "AudioDownload", "Failed", stopwatch.ElapsedMilliseconds);
                MarkAllStagesFailed(audioRecordId, stopwatch.ElapsedMilliseconds);
                return null;
            }
        }

        private void MarkAllStagesFailed(Guid audioRecordId, long elapsedMilliseconds)
        {
            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IAudioRepository>();

            var record = repository.GetById(audioRecordId);
            if (record is null)
                return;

            record.MarkAsFailed(elapsedMilliseconds);
            record.MarkTranscriptionAsFailed("The original audio could not be downloaded.", elapsedMilliseconds);
            record.MarkSummaryAsFailed("Summary was not attempted because the original audio could not be downloaded.", 0);
            repository.UpdateAudioRecord(record);
        }

        private void RunFlow<THandler>(
            string flowName,
            Guid audioRecordId,
            string correlationId,
            byte[] originalAudioBytes,
            Action<THandler, Guid, string, Stream> invoke)
            where THandler : notnull
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService<THandler>();
                using var originalStream = new MemoryStream(originalAudioBytes, writable: false);
                invoke(handler, audioRecordId, correlationId, originalStream);
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
