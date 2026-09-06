using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace AudioProcessor.Infrastructure.Realtime
{
    public class AudioStatusHub : Hub
    {
        public Task SubscribeToAudio(Guid audioId)
        {
            return Groups.AddToGroupAsync(Context.ConnectionId, audioId.ToString());
        }
    }
}
