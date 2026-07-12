using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AudioProcessor.Application.Interfaces
{
    public interface IAudioStorageService
    {
        Task<string> UploadAudioAsync(Stream fileStream, string fileName, string contentType, CancellationToken cancellationToken = default);
    }
}
