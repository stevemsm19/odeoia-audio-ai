using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AudioProcessor.Application.Interfaces;
using AudioProcessor.Domain.Enums;
using AudioProcessor.Application.Logging;
using Microsoft.Extensions.Logging;

namespace AudioProcessor.Application.UseCases
{
    // Transcription → Summary stage. The transcript lives only in memory, so a failed
    // transcription skips summarization entirely — there's nothing to fall back on.
    public class ProcessAudioSummaryCommandHandler
    {
        private readonly IAudioRepository _audioRepository;
        private readonly IAudioTranscriptionService _transcriptionService;
        private readonly IAudioSummarizationService _summarizationService;
        private readonly IAudioStorageService _audioStorageService;
        private readonly ILogger<ProcessAudioSummaryCommandHandler> _logger;

        public ProcessAudioSummaryCommandHandler(
            IAudioRepository audioRepository,
            IAudioTranscriptionService transcriptionService,
            IAudioSummarizationService summarizationService,
            IAudioStorageService audioStorageService,
            ILogger<ProcessAudioSummaryCommandHandler> logger)
        {
            _audioRepository = audioRepository;
            _transcriptionService = transcriptionService;
            _summarizationService = summarizationService;
            _audioStorageService = audioStorageService;
            _logger = logger;
        }

        // Failures in either stage are logged and persisted as Failed here, never rethrown.
        public async Task HandleAsync(Guid audioRecordId, Stream originalAudioStream, string correlationId, CancellationToken cancellationToken = default)
        {
            var total = Stopwatch.StartNew();
            var record = await _audioRepository.GetByIdAsync(audioRecordId, cancellationToken);
            if (record is null || record.SummaryStatus == AudioSummaryStatus.Completed)
                return;

            string? transcript = await RunTranscriptionAsync(record, originalAudioStream, correlationId, total, cancellationToken);
            if (transcript is null)
                return;

            await RunSummarizationAsync(record, transcript, correlationId, total, cancellationToken);
        }

        // Returns null if transcription failed (both stages already persisted as Failed).
        private async Task<string?> RunTranscriptionAsync(Domain.Entities.AudioRecord record, Stream originalAudioStream, string correlationId, Stopwatch total, CancellationToken cancellationToken)
        {
            var stageStopwatch = Stopwatch.StartNew();
            record.MarkTranscriptionAsProcessing();
            var databaseUpdate = Stopwatch.StartNew();
            await _audioRepository.UpdateAudioRecordAsync(record, cancellationToken);
            AudioPerformanceLog.Write(_logger, record.Id, "Transcription", correlationId, "DatabaseUpdate", "Completed", databaseUpdate.ElapsedMilliseconds);

            try
            {
                AudioTranscriptionResult result = await _transcriptionService.TranscribeAsync(originalAudioStream, record.FileName, cancellationToken);
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
