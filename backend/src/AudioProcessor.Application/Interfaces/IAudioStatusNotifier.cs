using System;
using System.Threading;
using System.Threading.Tasks;

namespace AudioProcessor.Application.Interfaces
{
    public interface IAudioStatusNotifier
    {
        Task NotifyStatusChangedAsync(Guid audioRecordId, CancellationToken cancellationToken = default);
    }
}
