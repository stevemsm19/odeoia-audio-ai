using System;

namespace AudioProcessor.Application.Interfaces
{
    /// <summary>
    /// Starts the two independent processing flows (Compression, Transcription→Summary)
    /// for an uploaded audio record, each as its own <see cref="System.Threading.Tasks.Task"/>.
    /// Returns immediately — the flows run and update their own state independently.
    /// </summary>
    public interface IAudioProcessingLauncher
    {
        void LaunchProcessing(Guid audioRecordId, string correlationId);
    }
}
