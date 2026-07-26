using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AudioProcessor.Application.Interfaces;
using AudioProcessor.Domain.Entities;
using AudioProcessor.Domain.Enums;
using Microsoft.Extensions.Logging;

//TODO: Divide in command handler and command

namespace AudioProcessor.Application.UseCases
{
    /// <summary>
    /// Command handler responsible for executing the asynchronous audio compression workflow.
    /// It streams the original audio from storage, runs compression, uploads the resulting AAC stream,
    /// and updates the record status and execution duration in the persistent repository.
    /// </summary>
    public class ProcessAudioCompressionCommandHandler
    {
        private readonly IAudioRepository _audioRepository;
        private readonly IAudioStorageService _audioStorageService;
        private readonly IAudioCompressionService _audioCompressionService;
        private readonly HttpClient _httpClient;
        private readonly ILogger<ProcessAudioCompressionCommandHandler> _logger;

        public ProcessAudioCompressionCommandHandler(
            IAudioRepository audioRepository,
            IAudioStorageService audioStorageService,
            IAudioCompressionService audioCompressionService,
            HttpClient httpClient,
            ILogger<ProcessAudioCompressionCommandHandler> logger)
        {
            _audioRepository = audioRepository ?? throw new ArgumentNullException(nameof(audioRepository));
            _audioStorageService = audioStorageService ?? throw new ArgumentNullException(nameof(audioStorageService));
            _audioCompressionService = audioCompressionService ?? throw new ArgumentNullException(nameof(audioCompressionService));
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Orchestrates the retrieval, compression, upload, and status transition of the specified audio record.
        /// </summary>
        /// <param name="audioRecordId">The unique identifier of the audio record to process.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <exception cref="Exception">Propagates any encountered transient or processing errors to ensure the queue consumer can handle them.</exception>
        public async Task HandleAsync(Guid audioRecordId, CancellationToken cancellationToken = default)
        {
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
            await _audioRepository.UpdateAudioRecordAsync(record, cancellationToken);

            Stream? originalStream = null;
            Stream? compressedStream = null;

            try
            {
                _logger.LogInformation("Streaming original audio file from: {Url}", record.StorageUrl);

                var response = await _httpClient.GetAsync(record.StorageUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();

                originalStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                _logger.LogInformation("Invoking audio compression service for {FileName}", record.FileName);
                compressedStream = await _audioCompressionService.CompressToAacAsync(originalStream, record.FileName, cancellationToken);

                string compressedFileName = Path.ChangeExtension(record.FileName, ".aac");
                _logger.LogInformation("Uploading compressed audio to storage as {FileName}", compressedFileName);

                string compressedStorageUrl = await _audioStorageService.UploadAudioAsync(
                    compressedStream,
                    compressedFileName,
                    "audio/aac",
                    record.Id,
                    cancellationToken);

                stopwatch.Stop();

                record.MarkAsCompleted(compressedStorageUrl, stopwatch.ElapsedMilliseconds);
                await _audioRepository.UpdateAudioRecordAsync(record, cancellationToken);

                _logger.LogInformation("Audio compression completed successfully for Record ID {Id} in {Duration} ms. URL: {Url}",
                    audioRecordId, stopwatch.ElapsedMilliseconds, compressedStorageUrl);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, "An error occurred during audio compression processing for Record ID: {Id}", audioRecordId);

                try
                {
                    record.MarkAsFailed(stopwatch.ElapsedMilliseconds);
                    await _audioRepository.UpdateAudioRecordAsync(record, cancellationToken);
                }
                catch (Exception dbEx)
                {
                    _logger.LogError(dbEx, "Failed to mark record {Id} as Failed in the database.", audioRecordId);
                }

                throw;
            }
            finally
            {
                if (originalStream != null)
                {
                    await originalStream.DisposeAsync();
                }
                if (compressedStream != null)
                {
                    await compressedStream.DisposeAsync();
                }
            }
        }
    }
}
