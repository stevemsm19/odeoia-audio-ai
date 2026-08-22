using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AudioProcessor.Api.Controllers;
using AudioProcessor.Application.Interfaces;
using AudioProcessor.Application.UseCases;
using AudioProcessor.Domain.Entities;
using AudioProcessor.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace AudioProcessor.Tests.Controllers
{
    /// <summary>
    /// The upload endpoint no longer starts processing itself — it only persists the
    /// Pending record and returns. AudioController has no dependency on
    /// IAudioProcessingLauncher at all anymore; these tests lock in that the response
    /// reflects an untouched, still-Pending record.
    /// </summary>
    public class AudioControllerUploadTests
    {
        private static IFormFile BuildFormFile(byte[] content, string fileName, string contentType)
        {
            var stream = new MemoryStream(content);
            return new FormFile(stream, 0, stream.Length, "file", fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = contentType
            };
        }

        [Fact]
        public async Task UploadAudioFile_ReturnsAcceptedImmediately_WithRecordStillPending()
        {
            var mockStorageService = new Mock<IAudioStorageService>();
            mockStorageService
                .Setup(s => s.UploadAudioAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync("https://example.supabase.co/audio-files/original.mp3");

            var mockRepository = new Mock<IAudioRepository>();
            mockRepository
                .Setup(r => r.SaveAudioRecordAsync(It.IsAny<AudioRecord>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var uploadUseCase = new UploadAudioUseCase(mockStorageService.Object, mockRepository.Object);
            var controller = new AudioController(uploadUseCase, mockRepository.Object);

            var file = BuildFormFile(new byte[] { 1, 2, 3, 4 }, "sample.mp3", "audio/mpeg");

            IActionResult result = await controller.UploadAudioFile(file, CancellationToken.None);

            var accepted = Assert.IsType<AcceptedResult>(result);
            var response = Assert.IsType<AudioUploadResponse>(accepted.Value);

            // Nothing advanced the record's state synchronously — only the background
            // processing service is allowed to move it past Pending.
            Assert.Equal(nameof(AudioProcessingStatus.Pending), response.Status);

            // The controller must persist exactly once and never touch UpdateAudioRecordAsync
            // itself — any state transition beyond the initial save belongs to the background
            // processing service, not the upload request.
            mockRepository.Verify(r => r.SaveAudioRecordAsync(It.IsAny<AudioRecord>(), It.IsAny<CancellationToken>()), Times.Once);
            mockRepository.Verify(r => r.UpdateAudioRecordAsync(It.IsAny<AudioRecord>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
