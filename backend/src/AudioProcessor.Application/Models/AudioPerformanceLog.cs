using System;
using Microsoft.Extensions.Logging;

namespace AudioProcessor.Application.Models
{
    public static class AudioPerformanceLog
    {
        public static void Write(ILogger logger, Guid audioId, AudioProcessingJobType jobType, string correlationId, string stage, string status, long elapsedMilliseconds)
        {
            logger.LogInformation(
                "AudioPerformance {TimestampUtc} {AudioId} {JobType} {CorrelationId} {Stage} {Status} {ElapsedMilliseconds}",
                DateTimeOffset.UtcNow, audioId, jobType, correlationId, stage, status, elapsedMilliseconds);
        }
    }
}
