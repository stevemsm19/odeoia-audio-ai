using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using AudioProcessor.Application.Interfaces;

namespace AudioProcessor.Infrastructure.Queue
{
    /// <summary>
    /// An in-memory queue implementation for background jobs using <see cref="System.Threading.Channels.Channel{T}"/>.
    /// It provides a thread-safe, bounded, and high-performance mechanism for producing and consuming tasks asynchronously.
    /// </summary>
    public class ChannelBackgroundJobQueue : IBackgroundJobQueue
    {
        private readonly Channel<Guid> _channel;

        public ChannelBackgroundJobQueue()
        {
            var options = new BoundedChannelOptions(100)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = true,
                SingleWriter = false
            };

            _channel = Channel.CreateBounded<Guid>(options);
        }

        /// <inheritdoc />
        public async ValueTask EnqueueJobAsync(Guid audioRecordId, CancellationToken cancellationToken = default)
        {
            if (audioRecordId == Guid.Empty)
                throw new ArgumentException("Audio record ID cannot be empty.", nameof(audioRecordId));

            await _channel.Writer.WriteAsync(audioRecordId, cancellationToken);
        }

        /// <inheritdoc />
        public async ValueTask<Guid> DequeueJobAsync(CancellationToken cancellationToken = default)
        {
            return await _channel.Reader.ReadAsync(cancellationToken);
        }
    }
}
