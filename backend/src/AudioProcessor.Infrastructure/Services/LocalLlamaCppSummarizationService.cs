using System;
using System.Diagnostics;
using System.Text.Json;
using AudioProcessor.Application.Interfaces;
using AudioProcessor.Infrastructure.ExternalServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AudioProcessor.Infrastructure.Services
{
    public class LocalLlamaCppSummarizationService : IAudioSummarizationService
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly LocalModelWorkerOptions _options;
        private readonly ILogger<LocalLlamaCppSummarizationService> _logger;

        public LocalLlamaCppSummarizationService(
            IOptions<LocalModelWorkerOptions> options,
            ILogger<LocalLlamaCppSummarizationService> logger)
        {
            _options = options.Value;
            _logger = logger;
        }

        public AudioSummarizationResult Summarize(string transcript)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = _options.PythonExecutable,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add(_options.SummarizationWorkerScriptPath);
            startInfo.ArgumentList.Add("--llama-cli");
            startInfo.ArgumentList.Add(_options.LlamaCliPath);
            startInfo.ArgumentList.Add("--llm-model");
            startInfo.ArgumentList.Add(_options.LlmModelPath);

            string stdout = RunWorkerProcess(startInfo, transcript);

            var response = JsonSerializer.Deserialize<WorkerResponse>(stdout, JsonOptions);
            if (response is null || string.IsNullOrWhiteSpace(response.Summary))
                throw new InvalidOperationException("Local summarization worker returned no summary.");

            return new AudioSummarizationResult(response.Summary, response.ElapsedMilliseconds);
        }

        private string RunWorkerProcess(ProcessStartInfo startInfo, string transcript)
        {
            using var process = new Process { StartInfo = startInfo };
            if (!process.Start())
                throw new InvalidOperationException("Unable to start the local summarization worker.");

            process.StandardInput.Write(transcript);
            process.StandardInput.Close();

            string stdout = process.StandardOutput.ReadToEnd();
            string stderr = process.StandardError.ReadToEnd();

            int timeoutMs = Math.Max(1, _options.TimeoutSeconds) * 1000;
            bool exited = process.WaitForExit(timeoutMs);
            if (!exited)
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to terminate the local summarization worker after a timeout.");
                }

                throw new TimeoutException($"Local summarization worker timed out after {_options.TimeoutSeconds}s.");
            }

            if (process.ExitCode != 0)
                throw new InvalidOperationException($"Local summarization worker exited with {process.ExitCode}: {stderr}");

            return stdout;
        }

        private sealed record WorkerResponse(string Summary, long ElapsedMilliseconds);
    }
}
