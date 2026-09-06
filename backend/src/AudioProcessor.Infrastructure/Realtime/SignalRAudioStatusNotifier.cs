using System;
using System.Threading;
using System.Threading.Tasks;
using AudioProcessor.Application.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace AudioProcessor.Infrastructure.Realtime
{
    public class SignalRAudioStatusNotifier : IAudioStatusNotifier
    {
        private readonly IHubContext<AudioStatusHub> _hubContext;

        public SignalRAudioStatusNotifier(IHubContext<AudioStatusHub> hubContext)
        {
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
        }

        public Task NotifyStatusChangedAsync(Guid audioRecordId, CancellationToken cancellationToken = default)
        {
            return _hubContext.Clients
                .Group(audioRecordId.ToString())
                .SendAsync("audioStatusChanged", audioRecordId.ToString(), cancellationToken);
        }
    }
}
