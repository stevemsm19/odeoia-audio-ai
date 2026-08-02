using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AudioProcessor.Application.Interfaces
{
    public interface IAudioSummaryService
    {
        Task<AudioSummaryResult> GenerateSummaryAsync(Stream audioStream, string sourceFileName, CancellationToken cancellationToken = default);
    }

    //TODO: move to domain or models
    public record AudioSummaryResult(string Summary, long SpeechToTextElapsedMilliseconds, long SummarizationElapsedMilliseconds);
}
