using System;
using System.Threading;
using System.Threading.Tasks;

namespace AudioProcessor.Application.Interfaces
{
    /// <summary>
    /// Runs the two independent processing flows (Compression, Transcription→Summary)
    /// for an uploaded audio record, each as its own <see cref="System.Threading.Tasks.Task"/>
    /// via <c>Task.Run</c> + <c>Task.WhenAll</c>. Completes only once both flows have
    /// reached a terminal state — callers that need to bound how many audios are
    /// processed at once (e.g. via <c>Parallel.ForEachAsync</c>) rely on this awaiting
    /// the full audio, not just starting it.
    /// </summary>
    public interface IAudioProcessingLauncher
    {
        Task ProcessAsync(Guid audioRecordId, string correlationId, CancellationToken cancellationToken = default);
    }
}
