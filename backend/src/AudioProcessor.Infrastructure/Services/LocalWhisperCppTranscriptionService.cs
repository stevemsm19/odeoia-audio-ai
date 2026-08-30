using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using AudioProcessor.Application.Interfaces;
using AudioProcessor.Infrastructure.ExternalServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AudioProcessor.Infrastructure.Services
{
    public class LocalWhisperCppTranscriptionService : IAudioTranscriptionService
    {
        private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

        private readonly LocalModelWorkerOptions _options;
        private readonly ILogger<LocalWhisperCppTranscriptionService> _logger;

        public LocalWhisperCppTranscriptionService(
            IOptions<LocalModelWorkerOptions> options,
            ILogger<LocalWhisperCppTranscriptionService> logger)
        {
            _options = options.Value;
            _logger = logger;
        }

        public AudioTranscriptionResult Transcribe(Stream audioStream, string sourceFileName)
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), $"audio-transcription-{Guid.NewGuid():N}");
            Directory.CreateDirectory(tempDirectory);

            try
            {
                string inputPath = Path.Combine(tempDirectory, $"input{Path.GetExtension(sourceFileName)}");
                using (var output = File.Create(inputPath))
                {
                    audioStream.CopyTo(output);
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

                string stdout = RunWorkerProcess(startInfo, "transcription");

                var response = JsonSerializer.Deserialize<WorkerResponse>(stdout, JsonOptions);
                if (response is null || string.IsNullOrWhiteSpace(response.Transcript))
                    throw new InvalidOperationException("Local transcription worker returned no transcript.");

                return new AudioTranscriptionResult(response.Transcript, response.ElapsedMilliseconds);
            }
            finally
            {
                try { Directory.Delete(tempDirectory, recursive: true); }
                catch (Exception ex) { _logger.LogWarning(ex, "Could not delete transcription temp directory {Directory}", tempDirectory); }
            }
        }

        private string RunWorkerProcess(ProcessStartInfo startInfo, string workerName)
        {
            using var process = new Process { StartInfo = startInfo };
            if (!process.Start())
                throw new InvalidOperationException($"Unable to start the local {workerName} worker.");

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
                    _logger.LogError(ex, "Failed to terminate the local {WorkerName} worker after a timeout.", workerName);
                }

                throw new TimeoutException($"Local {workerName} worker timed out after {_options.TimeoutSeconds}s.");
            }

            if (process.ExitCode != 0)
                throw new InvalidOperationException($"Local {workerName} worker exited with {process.ExitCode}: {stderr}");

            return stdout;
        }

        private sealed record WorkerResponse(string Transcript, long ElapsedMilliseconds);
    }
}
