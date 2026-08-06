namespace AudioProcessor.Infrastructure.ExternalServices
{
    public class ExternalProcessSemaphoreOptions
    {
        public const string SectionName = "ExternalProcessSemaphore";

        /// <summary>
        /// Global limit on how many heavy external processes (FFmpeg, STT, LLM) can be
        /// active at once, across all audios and both flows equally. Temporary value
        /// pending a real benchmark.
        /// </summary>
        public int MaxConcurrentProcesses { get; set; } = 2;
    }
}
