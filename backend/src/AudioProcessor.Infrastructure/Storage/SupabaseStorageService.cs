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
    /// <summary>
    /// Implements external object storage interactions by acting as an adapter for the Supabase Storage API.
    /// Manages byte streams, object paths, bucket authorization, and public URL generation.
    /// </summary>
    public class SupabaseStorageService : IAudioStorageService
    {
        private readonly Client _supabaseClient;
        private readonly string _bucketName;
        private readonly ILogger<SupabaseStorageService> _logger;

        // The Supabase.Storage SDK (v1.4.0) is not safe for concurrent uploads from a shared client
        // instance. StorageFileApi.UploadOrUpdate mutates a per-instance StorageHeader (a plain
        // Dictionary<string,string>) without synchronization, and HttpClientProgress.UploadAsync
        // calls client.DefaultRequestHeaders.Clear() followed by Add() on the shared HttpClient —
        // also without any lock. Under concurrent uploads these unsynchronized writes corrupt the
        // header state, which causes the NullReferenceException inside HttpConnection.WriteAsciiString.
        // Serializing all SDK upload calls to one at a time eliminates that window.
        // This semaphore is intentionally separate from the FFmpeg/STT/LLM one: that semaphore
        // throttles heavy external OS processes; this one guards a specific SDK thread-safety boundary.
        private static readonly SemaphoreSlim _supabaseUploadSemaphore = new(1, 1);

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
            await _supabaseUploadSemaphore.WaitAsync(cancellationToken);
            try
            {
                _logger.LogDebug("Acquired Supabase upload semaphore for path {Path}", uniqueObjectPath);
                await _supabaseClient.Storage
                    .From(_bucketName)
                    .Upload(fileBytes, uniqueObjectPath, new Supabase.Storage.FileOptions
                    {
                        ContentType = contentType,
                        Upsert = false
                    });
            }
            finally
            {
                _supabaseUploadSemaphore.Release();
                _logger.LogDebug("Released Supabase upload semaphore for path {Path}", uniqueObjectPath);
            }

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
