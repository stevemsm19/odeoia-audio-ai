using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AudioProcessor.Application.Interfaces
{
    // Compression stage contract.
    public interface IAudioCompressionService
    {
        Task<Stream> CompressToAacAsync(
            Stream sourceStream,
            string sourceFileName,
            CancellationToken cancellationToken = default);
    }
}
