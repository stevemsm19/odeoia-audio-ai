using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AudioProcessor.Application.Interfaces;
using AudioProcessor.Domain.Enums;
using AudioProcessor.Application.Models;
using Microsoft.Extensions.Logging;

namespace AudioProcessor.Application.UseCases
{
    public class ProcessAudioSummaryCommandHandler
    {
        private readonly IAudioRepository _audioRepository;
        private readonly IAudioSummaryService _audioSummaryService;
        private readonly HttpClient _httpClient;
        private readonly ILogger<ProcessAudioSummaryCommandHandler> _logger;

        public ProcessAudioSummaryCommandHandler(
            IAudioRepository audioRepository,
            IAudioSummaryService audioSummaryService,
            HttpClient httpClient,
            ILogger<ProcessAudioSummaryCommandHandler> logger)
        {
            _audioRepository = audioRepository;
            _audioSummaryService = audioSummaryService;
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task HandleAsync(Guid audioRecordId, string correlationId, CancellationToken cancellationToken = default)
        {
            var total = Stopwatch.StartNew();
            var record = await _audioRepository.GetByIdAsync(audioRecordId, cancellationToken);
            if (record is null || record.SummaryStatus == AudioSummaryStatus.Completed)
                return;

            var stopwatch = Stopwatch.StartNew();
            record.MarkSummaryAsProcessing();
            var databaseUpdate = Stopwatch.StartNew();
            await _audioRepository.UpdateAudioRecordAsync(record, cancellationToken);
            AudioPerformanceLog.Write(_logger, audioRecordId, AudioProcessingJobType.Summary, correlationId, "DatabaseUpdate", "Completed", databaseUpdate.ElapsedMilliseconds);

            try
            {
                var download = Stopwatch.StartNew();
                using var response = await _httpClient.GetAsync(record.StorageUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();
                await using var audioStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                AudioPerformanceLog.Write(_logger, audioRecordId, AudioProcessingJobType.Summary, correlationId, "AudioDownload", "Completed", download.ElapsedMilliseconds);
                var aiRequest = Stopwatch.StartNew();
                AudioSummaryResult result = await _audioSummaryService.GenerateSummaryAsync(audioStream, record.FileName, cancellationToken);
                AudioPerformanceLog.Write(_logger, audioRecordId, AudioProcessingJobType.Summary, correlationId, "AiRequest", "Completed", aiRequest.ElapsedMilliseconds);
                AudioPerformanceLog.Write(_logger, audioRecordId, AudioProcessingJobType.Summary, correlationId, "SpeechToText", "Completed", result.SpeechToTextElapsedMilliseconds);
                AudioPerformanceLog.Write(_logger, audioRecordId, AudioProcessingJobType.Summary, correlationId, "Summarization", "Completed", result.SummarizationElapsedMilliseconds);

                stopwatch.Stop();
                record.MarkSummaryAsCompleted(result.Summary, stopwatch.ElapsedMilliseconds);
                databaseUpdate.Restart();
                await _audioRepository.UpdateAudioRecordAsync(record, cancellationToken);
                AudioPerformanceLog.Write(_logger, audioRecordId, AudioProcessingJobType.Summary, correlationId, "DatabaseUpdate", "Completed", databaseUpdate.ElapsedMilliseconds);
                _logger.LogInformation("Summary completed for record {Id} in {DurationMs} ms", audioRecordId, stopwatch.ElapsedMilliseconds);
                AudioPerformanceLog.Write(_logger, audioRecordId, AudioProcessingJobType.Summary, correlationId, "EndToEnd", "Completed", total.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                record.MarkSummaryAsFailed(ex.Message, stopwatch.ElapsedMilliseconds);
                databaseUpdate.Restart();
                await _audioRepository.UpdateAudioRecordAsync(record, CancellationToken.None);
                AudioPerformanceLog.Write(_logger, audioRecordId, AudioProcessingJobType.Summary, correlationId, "DatabaseUpdate", "Completed", databaseUpdate.ElapsedMilliseconds);
                _logger.LogError(ex, "Summary processing failed for record {Id}", audioRecordId);
                AudioPerformanceLog.Write(_logger, audioRecordId, AudioProcessingJobType.Summary, correlationId, "EndToEnd", "Failed", total.ElapsedMilliseconds);
                throw;
            }
        }
    }
}
