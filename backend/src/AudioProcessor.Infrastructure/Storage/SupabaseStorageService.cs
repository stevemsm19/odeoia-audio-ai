using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AudioProcessor.Application.Interfaces;
using AppSupabaseOptions = AudioProcessor.Infrastructure.ExternalServices.SupabaseOptions;
using Microsoft.Extensions.Options;
using Supabase;

namespace AudioProcessor.Infrastructure.Storage
{
    public class SupabaseStorageService : IAudioStorageService
    {
        private readonly Client _supabaseClient;
        private readonly string _bucketName;

        public SupabaseStorageService(Client supabaseClient, IOptions<AppSupabaseOptions> options)
        {
            _supabaseClient = supabaseClient;
            _bucketName = options.Value.BucketName;
        }

        public async Task<string> UploadAudioAsync(Stream fileStream, string fileName, string contentType, CancellationToken cancellationToken = default)
        {
            byte[] fileBytes = await ReadStreamToBytesAsync(fileStream, cancellationToken);

            string uniqueObjectPath = $"{Guid.NewGuid()}/{fileName}";

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
