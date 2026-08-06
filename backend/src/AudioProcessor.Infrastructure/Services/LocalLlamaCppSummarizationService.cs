using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AudioProcessor.Application.Interfaces;
using AudioProcessor.Infrastructure.ExternalServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AudioProcessor.Infrastructure.Services
{
    /// <summary>
    /// Summarization stage of Flujo 2. Invokes <c>audio_summary_worker.py</c>
    /// (llama.cpp) as an independent subprocess, receiving the transcript over
    /// stdin — never written to disk or persisted.
    /// </summary>
    public class LocalLlamaCppSummarizationService : IAudioSummarizationService
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly LocalModelWorkerOptions _options;
        private readonly SemaphoreSlim _externalProcessSemaphore;
        private readonly ILogger<LocalLlamaCppSummarizationService> _logger;

        public LocalLlamaCppSummarizationService(
            IOptions<LocalModelWorkerOptions> options,
            SemaphoreSlim externalProcessSemaphore,
            ILogger<LocalLlamaCppSummarizationService> logger)
        {
            _options = options.Value;
            _externalProcessSemaphore = externalProcessSemaphore;
            _logger = logger;
        }

        public async Task<AudioSummarizationResult> SummarizeAsync(string transcript, CancellationToken cancellationToken = default)
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

            string stdout = await RunWorkerProcessAsync(startInfo, transcript, cancellationToken);

            var response = JsonSerializer.Deserialize<WorkerResponse>(stdout, JsonOptions);
            if (response is null || string.IsNullOrWhiteSpace(response.Summary))
                throw new InvalidOperationException("Local summarization worker returned no summary.");

            _logger.LogInformation("Local summarization completed in {ElapsedMs} ms", response.ElapsedMilliseconds);
            return new AudioSummarizationResult(response.Summary, response.ElapsedMilliseconds);
        }

        private async Task<string> RunWorkerProcessAsync(ProcessStartInfo startInfo, string transcript, CancellationToken cancellationToken)
        {
            // Deliberately blocking: runs on its own explicit Thread, not ordinary async I/O.
            _externalProcessSemaphore.Wait(cancellationToken);
            try
            {
                using var process = new Process { StartInfo = startInfo };
                if (!process.Start())
                    throw new InvalidOperationException("Unable to start the local summarization worker.");

                await process.StandardInput.WriteAsync(transcript);
                process.StandardInput.Close();

                Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
                Task<string> stderrTask = process.StandardError.ReadToEndAsync();

                using var cancellationRegistration = cancellationToken.Register(() =>
                {
                    if (!process.HasExited)
                        process.Kill(entireProcessTree: true);
                });

                bool exited = process.WaitForExit(TimeSpan.FromSeconds(Math.Max(1, _options.TimeoutSeconds)));
                if (!exited)
                {
                    process.Kill(entireProcessTree: true);
                    throw new TimeoutException($"Local summarization worker timed out after {_options.TimeoutSeconds}s.");
                }

                cancellationToken.ThrowIfCancellationRequested();

                string stdout = await stdoutTask;
                string stderr = await stderrTask;

                if (process.ExitCode != 0)
                    throw new InvalidOperationException($"Local summarization worker exited with {process.ExitCode}: {stderr}");

                return stdout;
            }
            finally
            {
                _externalProcessSemaphore.Release();
            }
        }

        private sealed record WorkerResponse(string Summary, long ElapsedMilliseconds);
    }
}
