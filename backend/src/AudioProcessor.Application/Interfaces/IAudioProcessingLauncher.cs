using System;

namespace AudioProcessor.Application.Interfaces
{
    /// <summary>
    /// Starts the two independent processing flows (Compression, Transcription→Summary)
    /// for an uploaded audio record, one explicit <see cref="System.Threading.Thread"/>
    /// per flow. Returns immediately — the flows run and update their own state
    /// independently.
    /// </summary>
    public interface IAudioProcessingLauncher
    {
        void LaunchProcessing(Guid audioRecordId, string correlationId);
    }
}
