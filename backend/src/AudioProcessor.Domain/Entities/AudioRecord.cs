using System;
using AudioProcessor.Domain.Enums;

namespace AudioProcessor.Domain.Entities
{
    public class AudioRecord
    {
        public Guid Id { get; private set; }
        public string StorageUrl { get; private set; }
        public string FileName { get; private set; }
        public DateTime CreatedAt { get; private set; }

        // Flujo 1 (Compression) — columns owned exclusively by this flow.
        public string? CompressedStorageUrl { get; private set; }
        public AudioProcessingStatus Status { get; private set; }
        public long? CompressionDurationMs { get; private set; }

        // Flujo 2, stage 1 (Transcription) — never persists the transcript itself,
        // only its outcome.
        public AudioTranscriptionStatus TranscriptionStatus { get; private set; }
        public long? TranscriptionDurationMs { get; private set; }
        public string? TranscriptionError { get; private set; }

        // Flujo 2, stage 2 (Summary) — stored as a Storage URL, not as text.
        public string? SummaryStorageUrl { get; private set; }
        public AudioSummaryStatus SummaryStatus { get; private set; }
        public long? SummaryDurationMs { get; private set; }
        public string? SummaryError { get; private set; }

        /// <summary>
        /// Computed, not stored. "Completed" means the three stages reached a
        /// terminal state — it is not a success verdict.
        /// </summary>
        public AudioOverallStatus OverallStatus
        {
            get
            {
                bool compressionTerminal = Status is AudioProcessingStatus.Completed or AudioProcessingStatus.Failed;
                bool transcriptionTerminal = TranscriptionStatus is AudioTranscriptionStatus.Completed or AudioTranscriptionStatus.Failed;
                bool summaryTerminal = SummaryStatus is AudioSummaryStatus.Completed or AudioSummaryStatus.Failed;

                if (compressionTerminal && transcriptionTerminal && summaryTerminal)
                    return AudioOverallStatus.Completed;

                bool anyStageStarted = Status != AudioProcessingStatus.Pending
                    || TranscriptionStatus != AudioTranscriptionStatus.Pending
                    || SummaryStatus != AudioSummaryStatus.Pending;

                return anyStageStarted ? AudioOverallStatus.Processing : AudioOverallStatus.Pending;
            }
        }

        private AudioRecord()
        {
            StorageUrl = string.Empty;
            FileName = string.Empty;
        }

        public AudioRecord(Guid id, string storageUrl, string fileName, DateTime createdAt)
        {
            if (id == Guid.Empty)
                throw new ArgumentException("Id cannot be empty.", nameof(id));

            if (string.IsNullOrWhiteSpace(storageUrl))
                throw new ArgumentException("Storage URL cannot be empty.", nameof(storageUrl));

            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("File name cannot be null or empty.", nameof(fileName));

            Id = id;
            StorageUrl = storageUrl;
            FileName = fileName;
            CreatedAt = createdAt;
            Status = AudioProcessingStatus.Pending;
            CompressedStorageUrl = null;
            CompressionDurationMs = null;
            TranscriptionStatus = AudioTranscriptionStatus.Pending;
            TranscriptionDurationMs = null;
            TranscriptionError = null;
            SummaryStorageUrl = null;
            SummaryStatus = AudioSummaryStatus.Pending;
            SummaryDurationMs = null;
            SummaryError = null;
        }

        public void MarkAsProcessing()
        {
            Status = AudioProcessingStatus.Processing;
        }

        public void MarkAsCompleted(string compressedStorageUrl, long compressionDurationMs)
        {
            if (string.IsNullOrWhiteSpace(compressedStorageUrl))
                throw new ArgumentException("Compressed storage URL cannot be empty.", nameof(compressedStorageUrl));

            if (compressionDurationMs < 0)
                throw new ArgumentOutOfRangeException(nameof(compressionDurationMs), "Compression duration cannot be negative.");

            CompressedStorageUrl = compressedStorageUrl;
            CompressionDurationMs = compressionDurationMs;
            Status = AudioProcessingStatus.Completed;
        }

        public void MarkAsFailed(long compressionDurationMs)
        {
            if (compressionDurationMs < 0)
                throw new ArgumentOutOfRangeException(nameof(compressionDurationMs), "Compression duration cannot be negative.");

            CompressionDurationMs = compressionDurationMs;
            Status = AudioProcessingStatus.Failed;
        }

        public void MarkTranscriptionAsProcessing()
        {
            if (TranscriptionStatus == AudioTranscriptionStatus.Completed)
                return;

            TranscriptionStatus = AudioTranscriptionStatus.Processing;
            TranscriptionError = null;
        }

        public void MarkTranscriptionAsCompleted(long durationMs)
        {
            if (durationMs < 0)
                throw new ArgumentOutOfRangeException(nameof(durationMs));

            TranscriptionDurationMs = durationMs;
            TranscriptionError = null;
            TranscriptionStatus = AudioTranscriptionStatus.Completed;
        }

        public void MarkTranscriptionAsFailed(string error, long durationMs)
        {
            if (durationMs < 0)
                throw new ArgumentOutOfRangeException(nameof(durationMs));

            TranscriptionDurationMs = durationMs;
            TranscriptionError = string.IsNullOrWhiteSpace(error) ? "Transcription failed." : error[..Math.Min(error.Length, 1000)];
            TranscriptionStatus = AudioTranscriptionStatus.Failed;
        }

        public void MarkSummaryAsProcessing()
        {
            if (SummaryStatus == AudioSummaryStatus.Completed)
                return;

            SummaryStatus = AudioSummaryStatus.Processing;
            SummaryError = null;
        }

        public void MarkSummaryAsCompleted(string summaryStorageUrl, long durationMs)
        {
            if (string.IsNullOrWhiteSpace(summaryStorageUrl))
                throw new ArgumentException("Summary storage URL cannot be empty.", nameof(summaryStorageUrl));

            if (durationMs < 0)
                throw new ArgumentOutOfRangeException(nameof(durationMs));

            SummaryStorageUrl = summaryStorageUrl;
            SummaryDurationMs = durationMs;
            SummaryError = null;
            SummaryStatus = AudioSummaryStatus.Completed;
        }

        public void MarkSummaryAsFailed(string error, long durationMs)
        {
            if (durationMs < 0)
                throw new ArgumentOutOfRangeException(nameof(durationMs));

            SummaryDurationMs = durationMs;
            SummaryError = string.IsNullOrWhiteSpace(error) ? "Summary generation failed." : error[..Math.Min(error.Length, 1000)];
            SummaryStatus = AudioSummaryStatus.Failed;
        }
    }
}
