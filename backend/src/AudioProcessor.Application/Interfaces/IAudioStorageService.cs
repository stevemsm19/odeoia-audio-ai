using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AudioProcessor.Application.Interfaces
{
    public interface IAudioStorageService
    {
        // Returns the public URL of the uploaded file.
        Task<string> UploadAudioAsync(Stream fileStream, string fileName, string contentType, Guid folderId, CancellationToken cancellationToken = default);
    }
}
