using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AudioProcessor.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace AudioProcessor.Infrastructure.Services
{
    public class FFmpegAudioNoiseReductionService : IAudioNoiseReductionService
    {
        private readonly SemaphoreSlim _externalProcessSemaphore;
        private readonly ILogger<FFmpegAudioNoiseReductionService> _logger;

        public FFmpegAudioNoiseReductionService(SemaphoreSlim externalProcessSemaphore, ILogger<FFmpegAudioNoiseReductionService> logger)
        {
            _externalProcessSemaphore = externalProcessSemaphore;
            _logger = logger;
        }

        public async Task<Stream> ReduceNoiseAsync(
            Stream compressedAudioStream,
            string compressedFileName,
            CancellationToken cancellationToken = default)
        {
            if (compressedAudioStream == null)
                throw new ArgumentNullException(nameof(compressedAudioStream));

            if (string.IsNullOrWhiteSpace(compressedFileName))
                throw new ArgumentException("Compressed file name cannot be null or empty.", nameof(compressedFileName));

            string uniqueId = Guid.NewGuid().ToString("N");
            string sourceExtension = Path.GetExtension(compressedFileName);
            string tempInputPath = Path.Combine(Path.GetTempPath(), $"input_{uniqueId}{sourceExtension}");
            string tempOutputPath = Path.Combine(Path.GetTempPath(), $"output_{uniqueId}.aac");

            _logger.LogInformation("Creating temporary files for noise reduction. Input: {InputPath}, Output: {OutputPath}",
                tempInputPath, tempOutputPath);

            try
            {
                using (var fileStream = new FileStream(tempInputPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await compressedAudioStream.CopyToAsync(fileStream, cancellationToken);
                }

                await RunFFmpegProcessAsync(tempInputPath, tempOutputPath, cancellationToken);

                var memoryStream = new MemoryStream();
                using (var fileStream = new FileStream(tempOutputPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    await fileStream.CopyToAsync(memoryStream, cancellationToken);
                }
                memoryStream.Position = 0;

                _logger.LogInformation("Successfully completed noise reduction. Resulting AAC stream size: {Size} bytes", memoryStream.Length);
                return memoryStream;
            }
            finally
            {
                DeleteTempFile(tempInputPath);
                DeleteTempFile(tempOutputPath);
            }
        }

        private async Task RunFFmpegProcessAsync(string inputPath, string outputPath, CancellationToken cancellationToken)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                // -af afftdn: FFT-based denoiser filter.
                // tn=1 makes it continuously track the noise floor from the audio itself
                // instead of assuming a fixed -50dB profile; nr/nf are raised so the
                // reduction is actually audible on real background noise (defaults are
                // tuned for faint hiss, not room/background noise).
                Arguments = $"-y -i \"{inputPath}\" -af afftdn=nr=25:nf=-35:tn=1 -c:a aac -b:a 192k \"{outputPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            await _externalProcessSemaphore.WaitAsync(cancellationToken);
            try
            {
                using var process = new Process { StartInfo = startInfo };
                var stderrBuilder = new StringBuilder();

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        stderrBuilder.AppendLine(e.Data);
                    }
                };

                _logger.LogInformation("Launching FFmpeg process with arguments: {Args}", startInfo.Arguments);

                if (!process.Start())
                {
                    throw new InvalidOperationException("Failed to start FFmpeg process. Is FFmpeg installed and in the system PATH?");
                }

                process.BeginErrorReadLine();
                process.BeginOutputReadLine();

                try
                {
                    await process.WaitForExitAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("Noise reduction cancelled. Terminating FFmpeg process...");
                    try
                    {
                        if (!process.HasExited)
                        {
                            process.Kill(entireProcessTree: true);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to terminate FFmpeg process tree during cancellation.");
                    }
                    throw;
                }

                if (process.ExitCode != 0)
                {
                    string errorLogs = stderrBuilder.ToString();
                    _logger.LogError("FFmpeg failed with exit code {ExitCode}. CLI Log: {Logs}", process.ExitCode, errorLogs);
                    throw new InvalidOperationException($"FFmpeg noise reduction failed with exit code {process.ExitCode}.");
                }
            }
            finally
            {
                _externalProcessSemaphore.Release();
            }
        }

        private void DeleteTempFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                    _logger.LogDebug("Temporary file deleted: {Path}", path);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete temporary file: {Path}", path);
            }
        }
    }
}
