using System;
using AudioProcessor.Domain.Enums;
using System.Globalization;

namespace AudioProcessor.Domain.Entities
{
    public class AudioRecord
    {
        public Guid Id { get; private set; }
        public string StorageUrl { get; private set; }
        public string FileName { get; private set; }
        public DateTime CreatedAt { get; private set; }
        public string? CompressedStorageUrl { get; private set; }
        public AudioProcessingStatus Status { get; private set; }
        public long? CompressionDurationMs { get; private set; }
        public string? Summary { get; private set; }
        public AudioSummaryStatus SummaryStatus { get; private set; }
        public long? SummaryDurationMs { get; private set; }
        public string? SummaryError { get; private set; }

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
            Summary = null;
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

        public void MarkSummaryAsProcessing()
        {
            if (SummaryStatus == AudioSummaryStatus.Completed)
                return;

            SummaryStatus = AudioSummaryStatus.Processing;
            SummaryError = null;
        }

        public void MarkSummaryAsCompleted(string summary, long durationMs)
        {
            if (durationMs < 0)
                throw new ArgumentOutOfRangeException(nameof(durationMs));

            Summary = LimitSummaryLength(summary);
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

        private static string LimitSummaryLength(string summary)
        {
            string normalized = string.Join(" ", (summary ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            if (string.IsNullOrWhiteSpace(normalized))
                return "No speech detected";

            int[] elements = StringInfo.ParseCombiningCharacters(normalized);
            if (elements.Length <= 500)
                return normalized;

            int take = 499;
            string candidate = normalized[..elements[take]].TrimEnd();
            int wordBoundary = candidate.LastIndexOf(' ');
            if (wordBoundary > 15)
                candidate = candidate[..wordBoundary];

            return candidate + "…";
        }
    }
}
