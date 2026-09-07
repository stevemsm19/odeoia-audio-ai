using System;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AudioProcessor.Application.Interfaces;
using AudioProcessor.Infrastructure.ExternalServices;
using Microsoft.Extensions.Options;

namespace AudioProcessor.Infrastructure.Services
{
    // Summary stage — runs llama.cpp (LLM) via a Python subprocess; transcript passed over stdin, never written to disk.
    public class LocalLlamaCppSummarizationService : IAudioSummarizationService
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly LocalModelWorkerOptions _options;
        private readonly SemaphoreSlim _externalProcessSemaphore;

        public LocalLlamaCppSummarizationService(
            IOptions<LocalModelWorkerOptions> options,
            SemaphoreSlim externalProcessSemaphore)
        {
            _options = options.Value;
            _externalProcessSemaphore = externalProcessSemaphore;
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

            return new AudioSummarizationResult(response.Summary, response.ElapsedMilliseconds);
        }

        private async Task<string> RunWorkerProcessAsync(ProcessStartInfo startInfo, string transcript, CancellationToken cancellationToken)
        {
            await _externalProcessSemaphore.WaitAsync(cancellationToken);
            try
            {
                using var process = new Process { StartInfo = startInfo };
                if (!process.Start())
                    throw new InvalidOperationException("Unable to start the local summarization worker.");

                await process.StandardInput.WriteAsync(transcript);
                process.StandardInput.Close();

                Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
                Task<string> stderrTask = process.StandardError.ReadToEndAsync();

                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Max(1, _options.TimeoutSeconds)));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

                try
                {
                    await process.WaitForExitAsync(linkedCts.Token);
                }
                catch (OperationCanceledException)
                {
                    if (!process.HasExited)
                        process.Kill(entireProcessTree: true);

                    if (cancellationToken.IsCancellationRequested)
                        throw;

                    throw new TimeoutException($"Local summarization worker timed out after {_options.TimeoutSeconds}s.");
                }

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
