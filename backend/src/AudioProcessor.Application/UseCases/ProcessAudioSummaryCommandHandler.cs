using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AudioProcessor.Application.Interfaces;
using AudioProcessor.Domain.Enums;
using AudioProcessor.Application.Logging;
using Microsoft.Extensions.Logging;

namespace AudioProcessor.Application.UseCases
{
    /// <summary>
    /// Orchestrates Flujo 2 (Transcription → Summary). The two stages are independent
    /// external process invocations: if transcription fails, summarization is never
    /// attempted, and there is no transcript to fall back on because it is never
    /// persisted.
    /// </summary>
    public class ProcessAudioSummaryCommandHandler
    {
        private readonly IAudioRepository _audioRepository;
        private readonly IAudioTranscriptionService _transcriptionService;
        private readonly IAudioSummarizationService _summarizationService;
        private readonly IAudioStorageService _audioStorageService;
        private readonly HttpClient _httpClient;
        private readonly ILogger<ProcessAudioSummaryCommandHandler> _logger;

        public ProcessAudioSummaryCommandHandler(
            IAudioRepository audioRepository,
            IAudioTranscriptionService transcriptionService,
            IAudioSummarizationService summarizationService,
            IAudioStorageService audioStorageService,
            HttpClient httpClient,
            ILogger<ProcessAudioSummaryCommandHandler> logger)
        {
            _audioRepository = audioRepository;
            _transcriptionService = transcriptionService;
            _summarizationService = summarizationService;
            _audioStorageService = audioStorageService;
            _httpClient = httpClient;
            _logger = logger;
        }

        /// <remarks>
        /// Failures in either stage are fully handled here (logged, persisted as <c>Failed</c>) and never rethrown.
        /// </remarks>
        public async Task HandleAsync(Guid audioRecordId, string correlationId, CancellationToken cancellationToken = default)
        {
            var total = Stopwatch.StartNew();
            var record = await _audioRepository.GetByIdAsync(audioRecordId, cancellationToken);
            if (record is null || record.SummaryStatus == AudioSummaryStatus.Completed)
                return;

            string? transcript = await RunTranscriptionAsync(record, correlationId, total, cancellationToken);
            if (transcript is null)
                return;

            await RunSummarizationAsync(record, transcript, correlationId, total, cancellationToken);
        }

        /// <returns>The transcript, or <c>null</c> if transcription failed (already
        /// persisted as Failed for both Transcription and Summary — nothing left to do).</returns>
        private async Task<string?> RunTranscriptionAsync(Domain.Entities.AudioRecord record, string correlationId, Stopwatch total, CancellationToken cancellationToken)
        {
            var stageStopwatch = Stopwatch.StartNew();
            record.MarkTranscriptionAsProcessing();
            var databaseUpdate = Stopwatch.StartNew();
            await _audioRepository.UpdateAudioRecordAsync(record, cancellationToken);
            AudioPerformanceLog.Write(_logger, record.Id, "Transcription", correlationId, "DatabaseUpdate", "Completed", databaseUpdate.ElapsedMilliseconds);

            try
            {
                var download = Stopwatch.StartNew();
                using var response = await _httpClient.GetAsync(record.StorageUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();
                await using var audioStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                AudioPerformanceLog.Write(_logger, record.Id, "Transcription", correlationId, "AudioDownload", "Completed", download.ElapsedMilliseconds);

                AudioTranscriptionResult result = await _transcriptionService.TranscribeAsync(audioStream, record.FileName, cancellationToken);
                AudioPerformanceLog.Write(_logger, record.Id, "Transcription", correlationId, "SpeechToText", "Completed", result.ElapsedMilliseconds);

                stageStopwatch.Stop();
                record.MarkTranscriptionAsCompleted(stageStopwatch.ElapsedMilliseconds);
                databaseUpdate.Restart();
                await _audioRepository.UpdateAudioRecordAsync(record, cancellationToken);
                AudioPerformanceLog.Write(_logger, record.Id, "Transcription", correlationId, "DatabaseUpdate", "Completed", databaseUpdate.ElapsedMilliseconds);
                AudioPerformanceLog.Write(_logger, record.Id, "Transcription", correlationId, "EndToEnd", "Completed", total.ElapsedMilliseconds);

                return result.Transcript;
            }
            catch (Exception ex)
            {
                stageStopwatch.Stop();
                _logger.LogError(ex, "Transcription failed for record {Id}", record.Id);

                // Transcription is a strict prerequisite for Summary, and the transcript
                // is never persisted, so there is nothing to reuse.
                record.MarkTranscriptionAsFailed(ex.Message, stageStopwatch.ElapsedMilliseconds);
                record.MarkSummaryAsFailed("Summary was not attempted because transcription failed.", 0);
                await _audioRepository.UpdateAudioRecordAsync(record, CancellationToken.None);
                AudioPerformanceLog.Write(_logger, record.Id, "Transcription", correlationId, "EndToEnd", "Failed", total.ElapsedMilliseconds);
                return null;
            }
        }

        private async Task RunSummarizationAsync(Domain.Entities.AudioRecord record, string transcript, string correlationId, Stopwatch total, CancellationToken cancellationToken)
        {
            var stageStopwatch = Stopwatch.StartNew();
            record.MarkSummaryAsProcessing();
            var databaseUpdate = Stopwatch.StartNew();
            await _audioRepository.UpdateAudioRecordAsync(record, cancellationToken);
            AudioPerformanceLog.Write(_logger, record.Id, "Summary", correlationId, "DatabaseUpdate", "Completed", databaseUpdate.ElapsedMilliseconds);

            try
            {
                AudioSummarizationResult result = await _summarizationService.SummarizeAsync(transcript, cancellationToken);
                AudioPerformanceLog.Write(_logger, record.Id, "Summary", correlationId, "Summarization", "Completed", result.ElapsedMilliseconds);

                var upload = Stopwatch.StartNew();
                byte[] summaryBytes = Encoding.UTF8.GetBytes(result.Summary);
                await using var summaryStream = new MemoryStream(summaryBytes);
                string summaryStorageUrl = await _audioStorageService.UploadAudioAsync(summaryStream, "summary.txt", "text/plain", record.Id, cancellationToken);
                AudioPerformanceLog.Write(_logger, record.Id, "Summary", correlationId, "SummaryUpload", "Completed", upload.ElapsedMilliseconds);

                stageStopwatch.Stop();
                record.MarkSummaryAsCompleted(summaryStorageUrl, stageStopwatch.ElapsedMilliseconds);
                databaseUpdate.Restart();
                await _audioRepository.UpdateAudioRecordAsync(record, cancellationToken);
                AudioPerformanceLog.Write(_logger, record.Id, "Summary", correlationId, "DatabaseUpdate", "Completed", databaseUpdate.ElapsedMilliseconds);
                AudioPerformanceLog.Write(_logger, record.Id, "Summary", correlationId, "EndToEnd", "Completed", total.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                stageStopwatch.Stop();
                _logger.LogError(ex, "Summarization failed for record {Id}", record.Id);
                record.MarkSummaryAsFailed(ex.Message, stageStopwatch.ElapsedMilliseconds);
                await _audioRepository.UpdateAudioRecordAsync(record, CancellationToken.None);
                AudioPerformanceLog.Write(_logger, record.Id, "Summary", correlationId, "EndToEnd", "Failed", total.ElapsedMilliseconds);
            }
        }
    }
}
