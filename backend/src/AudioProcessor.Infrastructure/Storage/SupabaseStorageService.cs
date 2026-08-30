using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using AudioProcessor.Application.Interfaces;
using AppSupabaseOptions = AudioProcessor.Infrastructure.ExternalServices.SupabaseOptions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

namespace AudioProcessor.Infrastructure.Storage
{
    public class SupabaseStorageService : IAudioStorageService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _baseUrl;
        private readonly string _apiKey;
        private readonly string _bucketName;
        private readonly ILogger<SupabaseStorageService> _logger;

        public SupabaseStorageService(
            IHttpClientFactory httpClientFactory,
            IOptions<AppSupabaseOptions> options,
            ILogger<SupabaseStorageService> logger)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            var supabaseOptions = options?.Value ?? throw new ArgumentException("Supabase options are required.");
            _baseUrl = string.IsNullOrWhiteSpace(supabaseOptions.Url) ? throw new ArgumentException("Url option is required.") : supabaseOptions.Url.TrimEnd('/');
            _apiKey = string.IsNullOrWhiteSpace(supabaseOptions.Key) ? throw new ArgumentException("Key option is required.") : supabaseOptions.Key;
            _bucketName = string.IsNullOrWhiteSpace(supabaseOptions.BucketName) ? throw new ArgumentException("BucketName option is required.") : supabaseOptions.BucketName;
            _logger = logger;
        }

        public string UploadAudio(Stream fileStream, string fileName, string contentType, Guid folderId)
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

            byte[] fileBytes = ReadStreamToBytes(fileStream);

            string uniqueObjectPath = $"{folderId}/{fileName}";

            using var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/storage/v1/object/{_bucketName}/{uniqueObjectPath}")
            {
                Content = new ByteArrayContent(fileBytes)
            };
            request.Headers.Add("apikey", _apiKey);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

            using HttpClient httpClient = _httpClientFactory.CreateClient();
            using HttpResponseMessage response = httpClient.Send(request);

            if (!response.IsSuccessStatusCode)
            {
                string errorBody = new StreamReader(response.Content.ReadAsStream()).ReadToEnd();
                throw new HttpRequestException(
                    $"Supabase Storage upload failed with status {(int)response.StatusCode} ({response.StatusCode}): {errorBody}");
            }

            string publicUrl = $"{_baseUrl}/storage/v1/object/public/{_bucketName}/{uniqueObjectPath}";

            _logger.LogInformation("Successfully uploaded {FileName} to path {Path}. Public URL: {Url}",
                fileName, uniqueObjectPath, publicUrl);

            return publicUrl;
        }

        private static byte[] ReadStreamToBytes(Stream stream)
        {
            using var memoryStream = new MemoryStream();
            stream.CopyTo(memoryStream);
            return memoryStream.ToArray();
        }
    }
}
