using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using AudioProcessor.Application.Interfaces;
using AudioProcessor.Domain.Enums;
using AudioProcessor.Application.Logging;
using Microsoft.Extensions.Logging;

namespace AudioProcessor.Application.UseCases
{
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

        public void Handle(Guid audioRecordId, Stream originalAudioStream, string correlationId)
        {
            var total = Stopwatch.StartNew();
            var record = _audioRepository.GetById(audioRecordId);
            if (record is null || record.SummaryStatus == AudioSummaryStatus.Completed)
                return;

            string? transcript = RunTranscription(record, originalAudioStream, correlationId, total);
            if (transcript is null)
                return;

            RunSummarization(record, transcript, correlationId, total);
        }

        private string? RunTranscription(Domain.Entities.AudioRecord record, Stream originalAudioStream, string correlationId, Stopwatch total)
        {
            var stageStopwatch = Stopwatch.StartNew();
            record.MarkTranscriptionAsProcessing();
            var databaseUpdate = Stopwatch.StartNew();
            _audioRepository.UpdateAudioRecord(record);
            AudioPerformanceLog.Write(_logger, record.Id, "Transcription", correlationId, "DatabaseUpdate", "Completed", databaseUpdate.ElapsedMilliseconds);

            try
            {
                AudioTranscriptionResult result = _transcriptionService.Transcribe(originalAudioStream, record.FileName);
                AudioPerformanceLog.Write(_logger, record.Id, "Transcription", correlationId, "SpeechToText", "Completed", result.ElapsedMilliseconds);

                stageStopwatch.Stop();
                record.MarkTranscriptionAsCompleted(stageStopwatch.ElapsedMilliseconds);
                databaseUpdate.Restart();
                _audioRepository.UpdateAudioRecord(record);
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
                _audioRepository.UpdateAudioRecord(record);
                AudioPerformanceLog.Write(_logger, record.Id, "Transcription", correlationId, "EndToEnd", "Failed", total.ElapsedMilliseconds);
                return null;
            }
        }

        private void RunSummarization(Domain.Entities.AudioRecord record, string transcript, string correlationId, Stopwatch total)
        {
            var stageStopwatch = Stopwatch.StartNew();
            record.MarkSummaryAsProcessing();
            var databaseUpdate = Stopwatch.StartNew();
            _audioRepository.UpdateAudioRecord(record);
            AudioPerformanceLog.Write(_logger, record.Id, "Summary", correlationId, "DatabaseUpdate", "Completed", databaseUpdate.ElapsedMilliseconds);

            try
            {
                AudioSummarizationResult result = _summarizationService.Summarize(transcript);
                AudioPerformanceLog.Write(_logger, record.Id, "Summary", correlationId, "Summarization", "Completed", result.ElapsedMilliseconds);

                var upload = Stopwatch.StartNew();
                byte[] summaryBytes = Encoding.UTF8.GetBytes(result.Summary);
                using var summaryStream = new MemoryStream(summaryBytes);
                string summaryStorageUrl = _audioStorageService.UploadAudio(summaryStream, "summary.txt", "text/plain", record.Id);
                AudioPerformanceLog.Write(_logger, record.Id, "Summary", correlationId, "SummaryUpload", "Completed", upload.ElapsedMilliseconds);

                stageStopwatch.Stop();
                record.MarkSummaryAsCompleted(summaryStorageUrl, stageStopwatch.ElapsedMilliseconds);
                databaseUpdate.Restart();
                _audioRepository.UpdateAudioRecord(record);
                AudioPerformanceLog.Write(_logger, record.Id, "Summary", correlationId, "DatabaseUpdate", "Completed", databaseUpdate.ElapsedMilliseconds);
                AudioPerformanceLog.Write(_logger, record.Id, "Summary", correlationId, "EndToEnd", "Completed", total.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                stageStopwatch.Stop();
                _logger.LogError(ex, "Summarization failed for record {Id}", record.Id);
                record.MarkSummaryAsFailed(ex.Message, stageStopwatch.ElapsedMilliseconds);
                _audioRepository.UpdateAudioRecord(record);
                AudioPerformanceLog.Write(_logger, record.Id, "Summary", correlationId, "EndToEnd", "Failed", total.ElapsedMilliseconds);
            }
        }
    }
}
