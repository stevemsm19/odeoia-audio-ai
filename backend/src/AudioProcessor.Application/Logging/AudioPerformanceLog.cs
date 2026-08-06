using Microsoft.Extensions.Logging;

namespace AudioProcessor.Application.Logging
{
    /// <summary>
    /// Single structured performance-log mechanism for the processing flows;
    /// call sites must not also emit a free-form log line for the same event.
    /// Lives in Application, not Infrastructure, since Application must not
    /// reference Infrastructure and its only callers are Application/UseCases handlers.
    /// </summary>
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
