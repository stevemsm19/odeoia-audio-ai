using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AudioProcessor.Application.Interfaces;
using AudioProcessor.Domain.Entities;
using AudioProcessor.Domain.Enums;
using AudioProcessor.Application.Logging;
using Microsoft.Extensions.Logging;

//TODO: Divide in command handler and command

namespace AudioProcessor.Application.UseCases
{
    /// <summary>
    /// Command handler responsible for executing the asynchronous audio compression workflow.
    /// It compresses the original audio stream, uploads the resulting AAC stream,
    /// and updates the record status and execution duration in the persistent repository.
    /// </summary>
    public class ProcessAudioCompressionCommandHandler
    {
        private readonly IAudioRepository _audioRepository;
        private readonly IAudioStorageService _audioStorageService;
        private readonly IAudioCompressionService _audioCompressionService;
        private readonly IAudioNoiseReductionService _audioNoiseReductionService;
        private readonly ILogger<ProcessAudioCompressionCommandHandler> _logger;

        public ProcessAudioCompressionCommandHandler(
            IAudioRepository audioRepository,
            IAudioStorageService audioStorageService,
            IAudioCompressionService audioCompressionService,
            IAudioNoiseReductionService audioNoiseReductionService,
            ILogger<ProcessAudioCompressionCommandHandler> logger)
        {
            _audioRepository = audioRepository ?? throw new ArgumentNullException(nameof(audioRepository));
            _audioStorageService = audioStorageService ?? throw new ArgumentNullException(nameof(audioStorageService));
            _audioCompressionService = audioCompressionService ?? throw new ArgumentNullException(nameof(audioCompressionService));
            _audioNoiseReductionService = audioNoiseReductionService ?? throw new ArgumentNullException(nameof(audioNoiseReductionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Orchestrates the compression, upload, and status transition of the specified audio record.
        /// </summary>
        /// <param name="audioRecordId">The unique identifier of the audio record to process.</param>
        /// <param name="originalAudioStream">The original audio, already downloaded by the caller and shared with the other flow.</param>
        /// <param name="correlationId">Identifier used to correlate performance log entries across both flows for the same request.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <remarks>
        /// Failures are fully handled here (logged, persisted as <c>Failed</c>) and never rethrown.
        /// </remarks>
        public async Task HandleAsync(Guid audioRecordId, Stream originalAudioStream, string correlationId = "none", CancellationToken cancellationToken = default)
        {
            var total = Stopwatch.StartNew();
            _logger.LogInformation("Processing audio compression request for Record ID: {Id}", audioRecordId);

            var record = await _audioRepository.GetByIdAsync(audioRecordId, cancellationToken);
            if (record == null)
            {
                _logger.LogError("Audio record with ID {Id} was not found in the database. Aborting compression job.", audioRecordId);
                return;
            }

            if (record.Status == AudioProcessingStatus.Completed)
            {
                _logger.LogWarning("Audio record {Id} is already in Completed status. Skipping compression.", audioRecordId);
                return;
            }

            var stopwatch = Stopwatch.StartNew();
            record.MarkAsProcessing();
            var databaseUpdate = Stopwatch.StartNew();
            await _audioRepository.UpdateAudioRecordAsync(record, cancellationToken);
            AudioPerformanceLog.Write(_logger, audioRecordId, "Compression", correlationId, "DatabaseUpdate", "Completed", databaseUpdate.ElapsedMilliseconds);

            Stream? compressedStream = null;

            try
            {
                var compression = Stopwatch.StartNew();
                compressedStream = await _audioCompressionService.CompressToAacAsync(originalAudioStream, record.FileName, cancellationToken);
                AudioPerformanceLog.Write(_logger, audioRecordId, "Compression", correlationId, "Compression", "Completed", compression.ElapsedMilliseconds);

                string compressedFileName = Path.ChangeExtension(record.FileName, ".aac");

                var upload = Stopwatch.StartNew();
                string compressedStorageUrl = await _audioStorageService.UploadAudioAsync(
                    compressedStream,
                    compressedFileName,
                    "audio/aac",
                    record.Id,
                    cancellationToken);
                AudioPerformanceLog.Write(_logger, audioRecordId, "Compression", correlationId, "CompressedAudioUpload", "Completed", upload.ElapsedMilliseconds);

                stopwatch.Stop();

                record.MarkAsCompleted(compressedStorageUrl, stopwatch.ElapsedMilliseconds);
                databaseUpdate.Restart();
                await _audioRepository.UpdateAudioRecordAsync(record, cancellationToken);
                AudioPerformanceLog.Write(_logger, audioRecordId, "Compression", correlationId, "DatabaseUpdate", "Completed", databaseUpdate.ElapsedMilliseconds);

                await RunNoiseReductionAsync(record, compressedStream, compressedFileName, correlationId, cancellationToken);

                AudioPerformanceLog.Write(_logger, audioRecordId, "Compression", correlationId, "EndToEnd", "Completed", total.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "An error occurred during audio compression processing for Record ID: {Id}", audioRecordId);

                try
                {
                    record.MarkAsFailed(stopwatch.ElapsedMilliseconds);
                    databaseUpdate.Restart();
                    await _audioRepository.UpdateAudioRecordAsync(record, cancellationToken);
                    AudioPerformanceLog.Write(_logger, audioRecordId, "Compression", correlationId, "DatabaseUpdate", "Completed", databaseUpdate.ElapsedMilliseconds);
                }
                catch (Exception dbEx)
                {
                    _logger.LogError(dbEx, "Failed to mark record {Id} as Failed in the database.", audioRecordId);
                }

                AudioPerformanceLog.Write(_logger, audioRecordId, "Compression", correlationId, "EndToEnd", "Failed", total.ElapsedMilliseconds);
            }
            finally
            {
                if (compressedStream != null)
                {
                    await compressedStream.DisposeAsync();
                }
            }
        }

        private async Task RunNoiseReductionAsync(
            AudioRecord record,
            Stream compressedStream,
            string compressedFileName,
            string correlationId,
            CancellationToken cancellationToken)
        {
            var stopwatch = Stopwatch.StartNew();
            Stream? filteredStream = null;

            try
            {
                record.MarkNoiseReductionAsProcessing();
                await _audioRepository.UpdateAudioRecordAsync(record, cancellationToken);

                compressedStream.Position = 0;

                var noiseReduction = Stopwatch.StartNew();
                filteredStream = await _audioNoiseReductionService.ReduceNoiseAsync(compressedStream, compressedFileName, cancellationToken);
                AudioPerformanceLog.Write(_logger, record.Id, "NoiseReduction", correlationId, "NoiseReduction", "Completed", noiseReduction.ElapsedMilliseconds);

                var upload = Stopwatch.StartNew();
                string filteredStorageUrl = await _audioStorageService.UploadAudioAsync(
                    filteredStream,
                    "filtered.aac",
                    "audio/aac",
                    record.Id,
                    cancellationToken);
                AudioPerformanceLog.Write(_logger, record.Id, "NoiseReduction", correlationId, "FilteredAudioUpload", "Completed", upload.ElapsedMilliseconds);

                stopwatch.Stop();
                record.MarkNoiseReductionAsCompleted(filteredStorageUrl, stopwatch.ElapsedMilliseconds);
                await _audioRepository.UpdateAudioRecordAsync(record, cancellationToken);
                AudioPerformanceLog.Write(_logger, record.Id, "NoiseReduction", correlationId, "EndToEnd", "Completed", stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "An error occurred during noise reduction processing for Record ID: {Id}", record.Id);

                try
                {
                    record.MarkNoiseReductionAsFailed(ex.Message, stopwatch.ElapsedMilliseconds);
                    await _audioRepository.UpdateAudioRecordAsync(record, cancellationToken);
                }
                catch (Exception dbEx)
                {
                    _logger.LogError(dbEx, "Failed to mark record {Id} as Failed for noise reduction in the database.", record.Id);
                }

                AudioPerformanceLog.Write(_logger, record.Id, "NoiseReduction", correlationId, "EndToEnd", "Failed", stopwatch.ElapsedMilliseconds);
            }
            finally
            {
                if (filteredStream != null)
                {
                    await filteredStream.DisposeAsync();
                }
            }
        }
    }
}
