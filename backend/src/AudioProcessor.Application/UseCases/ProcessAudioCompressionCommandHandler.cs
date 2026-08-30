using System;
using System.Diagnostics;
using System.IO;
using AudioProcessor.Application.Interfaces;
using AudioProcessor.Domain.Enums;
using AudioProcessor.Application.Logging;
using Microsoft.Extensions.Logging;

namespace AudioProcessor.Application.UseCases
{
    public class ProcessAudioCompressionCommandHandler
    {
        private readonly IAudioRepository _audioRepository;
        private readonly IAudioStorageService _audioStorageService;
        private readonly IAudioCompressionService _audioCompressionService;
        private readonly ILogger<ProcessAudioCompressionCommandHandler> _logger;

        public ProcessAudioCompressionCommandHandler(
            IAudioRepository audioRepository,
            IAudioStorageService audioStorageService,
            IAudioCompressionService audioCompressionService,
            ILogger<ProcessAudioCompressionCommandHandler> logger)
        {
            _audioRepository = audioRepository ?? throw new ArgumentNullException(nameof(audioRepository));
            _audioStorageService = audioStorageService ?? throw new ArgumentNullException(nameof(audioStorageService));
            _audioCompressionService = audioCompressionService ?? throw new ArgumentNullException(nameof(audioCompressionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }
        public void Handle(Guid audioRecordId, Stream originalAudioStream, string correlationId = "none")
        {
            var total = Stopwatch.StartNew();
            _logger.LogInformation("Processing audio compression request for Record ID: {Id}", audioRecordId);

            var record = _audioRepository.GetById(audioRecordId);
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
            _audioRepository.UpdateAudioRecord(record);
            AudioPerformanceLog.Write(_logger, audioRecordId, "Compression", correlationId, "DatabaseUpdate", "Completed", databaseUpdate.ElapsedMilliseconds);

            Stream? compressedStream = null;

            try
            {
                var compression = Stopwatch.StartNew();
                compressedStream = _audioCompressionService.CompressToAac(originalAudioStream, record.FileName);
                AudioPerformanceLog.Write(_logger, audioRecordId, "Compression", correlationId, "Compression", "Completed", compression.ElapsedMilliseconds);

                string compressedFileName = Path.ChangeExtension(record.FileName, ".aac");

                var upload = Stopwatch.StartNew();
                string compressedStorageUrl = _audioStorageService.UploadAudio(
                    compressedStream,
                    compressedFileName,
                    "audio/aac",
                    record.Id);
                AudioPerformanceLog.Write(_logger, audioRecordId, "Compression", correlationId, "CompressedAudioUpload", "Completed", upload.ElapsedMilliseconds);

                stopwatch.Stop();

                record.MarkAsCompleted(compressedStorageUrl, stopwatch.ElapsedMilliseconds);
                databaseUpdate.Restart();
                _audioRepository.UpdateAudioRecord(record);
                AudioPerformanceLog.Write(_logger, audioRecordId, "Compression", correlationId, "DatabaseUpdate", "Completed", databaseUpdate.ElapsedMilliseconds);

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
                    _audioRepository.UpdateAudioRecord(record);
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
                compressedStream?.Dispose();
            }
        }
    }
}
