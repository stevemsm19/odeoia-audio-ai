using System.Threading;
using System.Threading.Tasks;
using AudioProcessor.Domain.Entities;

namespace AudioProcessor.Application.Interfaces
{
    public interface IAudioRepository
    {
        Task SaveAudioRecordAsync(AudioRecord audioRecord, CancellationToken cancellationToken = default);
    }
}
