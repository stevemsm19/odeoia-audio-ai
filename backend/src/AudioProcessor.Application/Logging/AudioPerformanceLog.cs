using Microsoft.Extensions.Logging;

namespace AudioProcessor.Application.Logging
{
    // Single structured performance-log entry point for all processing flows.
    public static class AudioPerformanceLog
    {
        public static void Write(ILogger logger, Guid audioId, string flow, string correlationId, string stage, string status, long elapsedMilliseconds)
        {
            logger.LogInformation(
                "AudioPerformance {TimestampUtc} {AudioId} {Flow} {CorrelationId} {Stage} {Status} {ElapsedMilliseconds}",
                DateTimeOffset.UtcNow, audioId, flow, correlationId, stage, status, elapsedMilliseconds);
        }
    }
}
