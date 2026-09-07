using System;
using System.Threading;
using System.Threading.Tasks;

namespace AudioProcessor.Application.Interfaces
{
    // Runs Compression and Transcription→Summary concurrently; completes only once both are done,
    // which callers rely on to bound how many audios are processed at once.
    public interface IAudioProcessingLauncher
    {
        Task ProcessAsync(Guid audioRecordId, string correlationId, CancellationToken cancellationToken = default);
    }
}
