using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using AudioProcessor.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace AudioProcessor.Infrastructure.Services
{
    public class FFmpegAudioCompressionService : IAudioCompressionService
    {
        private readonly ILogger<FFmpegAudioCompressionService> _logger;

        public FFmpegAudioCompressionService(ILogger<FFmpegAudioCompressionService> logger)
        {
            _logger = logger;
        }

        public Stream CompressToAac(Stream sourceStream, string sourceFileName)
        {
            if (sourceStream == null)
                throw new ArgumentNullException(nameof(sourceStream));

            if (string.IsNullOrWhiteSpace(sourceFileName))
                throw new ArgumentException("Source file name cannot be null or empty.", nameof(sourceFileName));

            string uniqueId = Guid.NewGuid().ToString("N");
            string sourceExtension = Path.GetExtension(sourceFileName);
            string tempInputPath = Path.Combine(Path.GetTempPath(), $"input_{uniqueId}{sourceExtension}");
            string tempOutputPath = Path.Combine(Path.GetTempPath(), $"output_{uniqueId}.aac");

            _logger.LogInformation("Creating temporary files for audio compression. Input: {InputPath}, Output: {OutputPath}",
                tempInputPath, tempOutputPath);

            try
            {
                using (var fileStream = new FileStream(tempInputPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    sourceStream.CopyTo(fileStream);
                }

                RunFFmpegProcess(tempInputPath, tempOutputPath);

                var memoryStream = new MemoryStream();
                using (var fileStream = new FileStream(tempOutputPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    fileStream.CopyTo(memoryStream);
                }
                memoryStream.Position = 0;

                _logger.LogInformation("Successfully completed compression. Resulting AAC stream size: {Size} bytes", memoryStream.Length);
                return memoryStream;
            }
            finally
            {
                DeleteTempFile(tempInputPath);
                DeleteTempFile(tempOutputPath);
            }
        }

        private void RunFFmpegProcess(string inputPath, string outputPath)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "ffmpeg",
                Arguments = $"-y -i \"{inputPath}\" -c:a aac -b:a 192k \"{outputPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

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

            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                string errorLogs = stderrBuilder.ToString();
                _logger.LogError("FFmpeg failed with exit code {ExitCode}. CLI Log: {Logs}", process.ExitCode, errorLogs);
                throw new InvalidOperationException($"FFmpeg compression failed with exit code {process.ExitCode}.");
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
