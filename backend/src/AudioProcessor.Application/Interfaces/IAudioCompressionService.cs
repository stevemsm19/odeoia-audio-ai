using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace AudioProcessor.Application.Interfaces
{
    /// <summary>
    /// Defines the contract for compressing audio streams into highly optimized formats.
    /// Typically used for down-sampling or transcoding audio to reduce storage footprint and transfer latency.
    /// </summary>
    public interface IAudioCompressionService
    {
        /// <summary>
        /// Compresses the provided audio stream to the AAC (Advanced Audio Coding) format.
        /// </summary>
        /// <param name="sourceStream">The input stream containing the raw or source audio data.</param>
        /// <param name="sourceFileName">The original file name of the audio source, used to determine its original format/extension.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A new readable <see cref="Stream"/> containing the compressed AAC audio data.</returns>
        Task<Stream> CompressToAacAsync(
            Stream sourceStream,
            string sourceFileName,
            CancellationToken cancellationToken = default);
    }
}
