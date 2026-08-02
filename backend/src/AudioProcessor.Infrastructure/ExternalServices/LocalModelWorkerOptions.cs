namespace AudioProcessor.Infrastructure.ExternalServices
{
    public class LocalModelWorkerOptions
    {
        public const string SectionName = "LocalModelWorker";

        public string PythonExecutable { get; set; } = "python3";
        public string WorkerScriptPath { get; set; } = "/app/workers/audio_summary_worker.py";
        public string WhisperCliPath { get; set; } = "whisper-cli";
        public string WhisperModelPath { get; set; } = "/models/ggml-tiny.en-q5_1.bin";
        public string LlamaCliPath { get; set; } = "llama-cli";
        public string LlmModelPath { get; set; } = "/models/qwen2.5-0.5b-instruct-q5_k_m.gguf";
        public int MaxConcurrentJobs { get; set; } = 1;
        public int TimeoutSeconds { get; set; } = 180;
    }
}
