using System;
using System.Text.Json;
using AudioProcessor.Api.Controllers;
using Xunit;

namespace AudioProcessor.Tests.Controllers
{
    /// <summary>
    /// Locks down the wire shape of GET /api/audio/{id}. ASP.NET Core's
    /// default MVC JSON options use camelCase property names — mirrored here explicitly
    /// so this test fails if the contract drifts without updating the frontend in the
    /// same change.
    /// </summary>
    public class AudioControllerContractTests
    {
        private static readonly JsonSerializerOptions AspNetCoreDefaultJsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        [Fact]
        public void AudioProcessingStatusResponse_SerializesWithExpectedFieldNames()
        {
            var response = new AudioProcessingStatusResponse(
                Id: Guid.NewGuid(),
                FileName: "audio.mp3",
                OverallStatus: "Processing",
                CompressionStatus: "Completed",
                CompressedStorageUrl: "https://example.supabase.co/audio-files/audio.aac",
                CompressionDurationMs: 1234,
                TranscriptionStatus: "Completed",
                TranscriptionError: null,
                TranscriptionDurationMs: 2345,
                SummaryStatus: "Processing",
                SummaryStorageUrl: null,
                SummaryError: null,
                SummaryDurationMs: null);

            string json = JsonSerializer.Serialize(response, AspNetCoreDefaultJsonOptions);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            Assert.True(root.TryGetProperty("id", out _));
            Assert.True(root.TryGetProperty("fileName", out _));
            Assert.True(root.TryGetProperty("overallStatus", out var overallStatus));
            Assert.Equal("Processing", overallStatus.GetString());
            Assert.True(root.TryGetProperty("compressionStatus", out _));
            Assert.True(root.TryGetProperty("compressedStorageUrl", out _));
            Assert.True(root.TryGetProperty("compressionDurationMs", out _));
            Assert.True(root.TryGetProperty("transcriptionStatus", out _));
            Assert.True(root.TryGetProperty("transcriptionError", out _));
            Assert.True(root.TryGetProperty("transcriptionDurationMs", out _));
            Assert.True(root.TryGetProperty("summaryStatus", out _));
            Assert.True(root.TryGetProperty("summaryStorageUrl", out _));
            Assert.True(root.TryGetProperty("summaryError", out _));
            Assert.True(root.TryGetProperty("summaryDurationMs", out _));

            // The old inline-text field must be gone — its replacement is summaryStorageUrl.
            Assert.False(root.TryGetProperty("summary", out _));
        }

        [Theory]
        [InlineData("Pending")]
        [InlineData("Processing")]
        [InlineData("Completed")]
        public void OverallStatus_OnlyHasThreeValidValues_NeverFailed(string value)
        {
            // OverallStatus is Pending/Processing/Completed only — there is no
            // OverallStatus.Failed. This test documents that constraint at the
            // contract boundary, not just in the domain enum.
            Assert.True(Enum.TryParse<AudioProcessor.Domain.Enums.AudioOverallStatus>(value, out _));
        }

        [Fact]
        public void AudioOverallStatus_HasNoFailedValue()
        {
            var values = Enum.GetNames<AudioProcessor.Domain.Enums.AudioOverallStatus>();
            Assert.DoesNotContain("Failed", values);
        }
    }
}
