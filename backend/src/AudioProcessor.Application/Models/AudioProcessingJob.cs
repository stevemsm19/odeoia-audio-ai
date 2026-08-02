using System;

namespace AudioProcessor.Application.Models
{
    public enum AudioProcessingJobType
    {
        Compression,
        Summary
    }

    public record AudioProcessingJob(Guid AudioRecordId, AudioProcessingJobType Type, string CorrelationId, DateTimeOffset EnqueuedAtUtc);
}
