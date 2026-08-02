using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AudioProcessor.Application.Interfaces;
using AudioProcessor.Infrastructure.ExternalServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AudioProcessor.Infrastructure.Services
{
    public class LocalWhisperCppSummaryService : IAudioSummaryService, IDisposable
    {
        private readonly LocalModelWorkerOptions _options;
        private readonly ILogger<LocalWhisperCppSummaryService> _logger;
        private readonly SemaphoreSlim _modelSlots;

        public LocalWhisperCppSummaryService(
            IOptions<LocalModelWorkerOptions> options,
            ILogger<LocalWhisperCppSummaryService> logger)
        {
            _options = options.Value;
            _logger = logger;
            _modelSlots = new SemaphoreSlim(Math.Max(1, _options.MaxConcurrentJobs));
        }

        public async Task<AudioSummaryResult> GenerateSummaryAsync(Stream audioStream, string sourceFileName, CancellationToken cancellationToken = default)
        {
            await _modelSlots.WaitAsync(cancellationToken);
            string tempDirectory = Path.Combine(Path.GetTempPath(), $"audio-summary-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDirectory);

            try
            {
                string inputPath = Path.Combine(tempDirectory, $"input{Path.GetExtension(sourceFileName)}");
                await using (var output = File.Create(inputPath))
                {
                    await audioStream.CopyToAsync(output, cancellationToken);
                }

                var startInfo = new ProcessStartInfo
                {
                    FileName = _options.PythonExecutable,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                startInfo.ArgumentList.Add(_options.WorkerScriptPath);
                startInfo.ArgumentList.Add("--input");
                startInfo.ArgumentList.Add(inputPath);
                startInfo.ArgumentList.Add("--whisper-cli");
                startInfo.ArgumentList.Add(_options.WhisperCliPath);
                startInfo.ArgumentList.Add("--whisper-model");
                startInfo.ArgumentList.Add(_options.WhisperModelPath);
                startInfo.ArgumentList.Add("--llama-cli");
                startInfo.ArgumentList.Add(_options.LlamaCliPath);
                startInfo.ArgumentList.Add("--llm-model");
                startInfo.ArgumentList.Add(_options.LlmModelPath);

                using var process = new Process { StartInfo = startInfo };
                if (!process.Start())
                    throw new InvalidOperationException("Unable to start the local audio summary worker.");

                Task<string> stdoutTask = process.StandardOutput.ReadToEndAsync();
                Task<string> stderrTask = process.StandardError.ReadToEndAsync();
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.TimeoutSeconds)));

                try
                {
                    await process.WaitForExitAsync(timeout.Token);
                }
                catch (OperationCanceledException)
                {
                    if (!process.HasExited)
                        process.Kill(entireProcessTree: true);
                    throw;
                }

                string stdout = await stdoutTask;
                string stderr = await stderrTask;
                if (process.ExitCode != 0)
                    throw new InvalidOperationException($"Local model worker exited with {process.ExitCode}: {stderr}");

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var response = JsonSerializer.Deserialize<WorkerResponse>(stdout, options);
                if (response is null || string.IsNullOrWhiteSpace(response.Summary))
                    throw new InvalidOperationException("Local model worker returned no summary.");

                _logger.LogInformation("Local Whisper.cpp and GGUF summary completed for {FileName}", sourceFileName);
                return new AudioSummaryResult(
                    LimitSummaryLength(response.Summary),
                    response.SpeechToTextElapsedMilliseconds,
                    response.SummarizationElapsedMilliseconds);
            }
            finally
            {
                _modelSlots.Release();
                try { Directory.Delete(tempDirectory, recursive: true); }
                catch (Exception ex) { _logger.LogWarning(ex, "Could not delete summary temp directory {Directory}", tempDirectory); }
            }
        }

        private static string LimitSummaryLength(string value)
        {
            string normalized = string.Join(" ", value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            if (string.IsNullOrWhiteSpace(normalized)) return "No speech detected";
            int[] indexes = StringInfo.ParseCombiningCharacters(normalized);
            if (indexes.Length <= 500) return normalized;
            string candidate = normalized[..indexes[499]].TrimEnd();
            int wordBoundary = candidate.LastIndexOf(' ');
            return (wordBoundary > 15 ? candidate[..wordBoundary] : candidate) + "…";
        }

        public void Dispose() => _modelSlots.Dispose();

        private sealed record WorkerResponse(string Summary, long SpeechToTextElapsedMilliseconds, long SummarizationElapsedMilliseconds);
    }
}
