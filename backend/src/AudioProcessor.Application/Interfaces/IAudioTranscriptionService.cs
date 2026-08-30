using System.IO;

namespace AudioProcessor.Application.Interfaces
{
    public interface IAudioTranscriptionService
    {
        AudioTranscriptionResult Transcribe(Stream audioStream, string sourceFileName);
    }

    public record AudioTranscriptionResult(string Transcript, long ElapsedMilliseconds);
}
