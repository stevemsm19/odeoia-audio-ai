using System;

namespace AudioProcessor.Domain.Entities
{
    public class AudioRecord
    {
        public Guid Id { get; private set; }
        public string StorageUrl { get; private set; }
        public string FileName { get; private set; }
        public DateTime CreatedAt { get; private set; }

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
                throw new ArgumentException("File name cannot be empty.", nameof(fileName));

            Id = id;
            StorageUrl = storageUrl;
            FileName = fileName;
            CreatedAt = createdAt;
        }
    }
}
