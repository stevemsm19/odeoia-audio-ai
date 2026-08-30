namespace AudioProcessor.Application.Interfaces
{
    public interface IAudioSummarizationService
    {
        AudioSummarizationResult Summarize(string transcript);
    }

    public record AudioSummarizationResult(string Summary, long ElapsedMilliseconds);
}
