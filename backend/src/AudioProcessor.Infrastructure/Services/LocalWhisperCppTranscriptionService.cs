using System;
using System.Diagnostics;
using System.IO;
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
    /// Speech-to-text stage of Flujo 2. Invokes <c>audio_transcription_worker.py</c>
    /// (whisper.cpp) as an independent subprocess — a separate invocation from
    /// summarization.
    /// </summary>
    public class LocalWhisperCppTranscriptionService : IAudioTranscriptionService
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly LocalModelWorkerOptions _options;
        private readonly SemaphoreSlim _externalProcessSemaphore;
        private readonly ILogger<LocalWhisperCppTranscriptionService> _logger;

        public LocalWhisperCppTranscriptionService(
            IOptions<LocalModelWorkerOptions> options,
            SemaphoreSlim externalProcessSemaphore,
            ILogger<LocalWhisperCppTranscriptionService> logger)
        {
            _options = options.Value;
            _externalProcessSemaphore = externalProcessSemaphore;
            _logger = logger;
        }

        public async Task<AudioTranscriptionResult> TranscribeAsync(Stream audioStream, string sourceFileName, CancellationToken cancellationToken = default)
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), $"audio-transcription-{Guid.NewGuid():N}");
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
                startInfo.ArgumentList.Add(_options.TranscriptionWorkerScriptPath);
                startInfo.ArgumentList.Add("--input");
                startInfo.ArgumentList.Add(inputPath);
                startInfo.ArgumentList.Add("--whisper-cli");
                startInfo.ArgumentList.Add(_options.WhisperCliPath);
                startInfo.ArgumentList.Add("--whisper-model");
                startInfo.ArgumentList.Add(_options.WhisperModelPath);

                string stdout = await RunWorkerProcessAsync(startInfo, "transcription", cancellationToken);

                var response = JsonSerializer.Deserialize<WorkerResponse>(stdout, JsonOptions);
                if (response is null || string.IsNullOrWhiteSpace(response.Transcript))
                    throw new InvalidOperationException("Local transcription worker returned no transcript.");

                _logger.LogInformation("Local transcription completed for {FileName} in {ElapsedMs} ms", sourceFileName, response.ElapsedMilliseconds);
                return new AudioTranscriptionResult(response.Transcript, response.ElapsedMilliseconds);
            }
            finally
            {
                try { Directory.Delete(tempDirectory, recursive: true); }
                catch (Exception ex) { _logger.LogWarning(ex, "Could not delete transcription temp directory {Directory}", tempDirectory); }
            }
        }

        private async Task<string> RunWorkerProcessAsync(ProcessStartInfo startInfo, string workerName, CancellationToken cancellationToken)
        {
            // Deliberately blocking: runs on its own explicit Thread, not ordinary async I/O.
            _externalProcessSemaphore.Wait(cancellationToken);
            try
            {
                using var process = new Process { StartInfo = startInfo };
                if (!process.Start())
                    throw new InvalidOperationException($"Unable to start the local {workerName} worker.");

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
                    throw new TimeoutException($"Local {workerName} worker timed out after {_options.TimeoutSeconds}s.");
                }

                cancellationToken.ThrowIfCancellationRequested();

                string stdout = await stdoutTask;
                string stderr = await stderrTask;

                if (process.ExitCode != 0)
                    throw new InvalidOperationException($"Local {workerName} worker exited with {process.ExitCode}: {stderr}");

                return stdout;
            }
            finally
            {
                _externalProcessSemaphore.Release();
            }
        }

        private sealed record WorkerResponse(string Transcript, long ElapsedMilliseconds);
    }
}
