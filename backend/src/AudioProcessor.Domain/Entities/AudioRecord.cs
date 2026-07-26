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
        public string? CompressedStorageUrl { get; private set; }
        public AudioProcessingStatus Status { get; private set; }
        public long? CompressionDurationMs { get; private set; }

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
    }
}
