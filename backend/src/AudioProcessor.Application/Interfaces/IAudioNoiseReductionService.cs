using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AudioProcessor.Application.Interfaces
{
    public interface IAudioNoiseReductionService
    {
        Task<Stream> ReduceNoiseAsync(
            Stream compressedAudioStream,
            string compressedFileName,
            CancellationToken cancellationToken = default);
    }
}
