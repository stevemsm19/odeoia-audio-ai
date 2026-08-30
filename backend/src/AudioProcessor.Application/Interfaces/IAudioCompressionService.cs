using System.IO;

namespace AudioProcessor.Application.Interfaces
{
    public interface IAudioCompressionService
    {
        Stream CompressToAac(Stream sourceStream, string sourceFileName);
    }
}
