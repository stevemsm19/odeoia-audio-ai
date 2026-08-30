using System;

namespace AudioProcessor.Application.Interfaces
{
    public interface IAudioProcessingLauncher
    {
        void Process(Guid audioRecordId, string correlationId);
    }
}
