namespace AudioProcessor.Infrastructure.ExternalServices
{
    public class LocalModelWorkerOptions
    {
        public const string SectionName = "LocalModelWorker";

        public string PythonExecutable { get; set; } = "python3";
        public string TranscriptionWorkerScriptPath { get; set; } = "/app/workers/audio_transcription_worker.py";
        public string SummarizationWorkerScriptPath { get; set; } = "/app/workers/audio_summary_worker.py";
        public string WhisperCliPath { get; set; } = "whisper-cli";
        public string WhisperModelPath { get; set; } = "/models/ggml-tiny.en-q5_1.bin";
        public string LlamaCliPath { get; set; } = "llama-cli";
        public string LlmModelPath { get; set; } = "/models/qwen2.5-0.5b-instruct-q5_k_m.gguf";
        public int TimeoutSeconds { get; set; } = 180;
    }
}
