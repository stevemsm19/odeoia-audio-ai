using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AudioProcessor.Application.Interfaces
{
    /// <summary>
    /// Speech-to-text step of Flujo 2, run as an independent external process
    /// invocation. The transcript it returns is a pass-through value for
    /// <see cref="IAudioSummarizationService"/> — callers must never persist it.
    /// </summary>
    public interface IAudioTranscriptionService
    {
        Task<AudioTranscriptionResult> TranscribeAsync(Stream audioStream, string sourceFileName, CancellationToken cancellationToken = default);
    }

    public record AudioTranscriptionResult(string Transcript, long ElapsedMilliseconds);
}
