using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AudioProcessor.Application.Interfaces;
using AppSupabaseOptions = AudioProcessor.Infrastructure.ExternalServices.SupabaseOptions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Supabase;

namespace AudioProcessor.Infrastructure.Storage
{
    // Supabase Storage adapter for uploaded audio/summary files.
    public class SupabaseStorageService : IAudioStorageService
    {
        private readonly Client _supabaseClient;
        private readonly string _bucketName;
        private readonly ILogger<SupabaseStorageService> _logger;
        public SupabaseStorageService(
            Client supabaseClient,
            IOptions<AppSupabaseOptions> options,
            ILogger<SupabaseStorageService> logger)
        {
            _supabaseClient = supabaseClient ?? throw new ArgumentNullException(nameof(supabaseClient));
            _bucketName = options?.Value?.BucketName ?? throw new ArgumentException("BucketName option is required.");
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<string> UploadAudioAsync(Stream fileStream, string fileName, string contentType, Guid folderId, CancellationToken cancellationToken = default)
        {
            if (fileStream == null)
                throw new ArgumentNullException(nameof(fileStream));

            if (string.IsNullOrWhiteSpace(fileName))
                throw new ArgumentException("File name cannot be empty.", nameof(fileName));

            string extension = Path.GetExtension(fileName).ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(contentType) || contentType == "application/octet-stream")
            {
                contentType = extension switch
                {
                    ".aac" => "audio/aac",
                    ".mp3" => "audio/mpeg",
                    ".wav" => "audio/wav",
                    _ => contentType ?? "application/octet-stream"
                };
            }

            _logger.LogInformation("Uploading file {FileName} with Content-Type {ContentType} (Extension: {Extension}) to Supabase bucket {Bucket}",
                fileName, contentType, extension, _bucketName);

            byte[] fileBytes = await ReadStreamToBytesAsync(fileStream, cancellationToken);

            string uniqueObjectPath = $"{folderId}/{fileName}";

            _logger.LogDebug("Waiting for Supabase upload semaphore for path {Path}", uniqueObjectPath);

            await _supabaseClient.Storage
                .From(_bucketName)
                .Upload(fileBytes, uniqueObjectPath, new Supabase.Storage.FileOptions
                {
                    ContentType = contentType,
                    Upsert = false
                });

            string publicUrl = _supabaseClient.Storage
                .From(_bucketName)
                .GetPublicUrl(uniqueObjectPath);

            _logger.LogInformation("Successfully uploaded {FileName} to path {Path}. Public URL: {Url}",
                fileName, uniqueObjectPath, publicUrl);

            return publicUrl;
        }

        private static async Task<byte[]> ReadStreamToBytesAsync(Stream stream, CancellationToken cancellationToken)
        {
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream, cancellationToken);
            return memoryStream.ToArray();
        }
    }
}
