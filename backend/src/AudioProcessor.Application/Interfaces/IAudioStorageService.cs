using System;
using System.IO;

namespace AudioProcessor.Application.Interfaces
{
    public interface IAudioStorageService
    {
        string UploadAudio(Stream fileStream, string fileName, string contentType, Guid folderId);
    }
}
