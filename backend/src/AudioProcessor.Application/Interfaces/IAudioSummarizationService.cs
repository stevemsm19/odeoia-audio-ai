using System.Threading;
using System.Threading.Tasks;

namespace AudioProcessor.Application.Interfaces
{
    /// <summary>
    /// LLM summarization step of Flujo 2, run as an independent external process
    /// invocation from <see cref="IAudioTranscriptionService"/>.
    /// </summary>
    public interface IAudioSummarizationService
    {
        Task<AudioSummarizationResult> SummarizeAsync(string transcript, CancellationToken cancellationToken = default);
    }

    public record AudioSummarizationResult(string Summary, long ElapsedMilliseconds);
}
