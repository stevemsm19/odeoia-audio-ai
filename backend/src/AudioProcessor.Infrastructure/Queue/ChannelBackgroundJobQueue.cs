using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using AudioProcessor.Application.Interfaces;
using AudioProcessor.Application.Models;

namespace AudioProcessor.Infrastructure.Queue
{
    /// <summary>
    /// An in-memory queue implementation for background jobs using <see cref="System.Threading.Channels.Channel{T}"/>.
    /// It provides a thread-safe, bounded, and high-performance mechanism for producing and consuming tasks asynchronously.
    /// </summary>
    public class ChannelBackgroundJobQueue : IBackgroundJobQueue
    {
        private readonly Channel<AudioProcessingJob> _channel;

        public ChannelBackgroundJobQueue()
        {
            var options = new BoundedChannelOptions(100)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = false
            };

            _channel = Channel.CreateBounded<AudioProcessingJob>(options);
        }

        /// <inheritdoc />
        public async ValueTask EnqueueJobAsync(AudioProcessingJob job, CancellationToken cancellationToken = default)
        {
            if (job is null || job.AudioRecordId == Guid.Empty)
                throw new ArgumentException("Audio processing job must contain a record ID.", nameof(job));

            await _channel.Writer.WriteAsync(job, cancellationToken);
        }

        /// <inheritdoc />
        public async ValueTask<AudioProcessingJob> DequeueJobAsync(CancellationToken cancellationToken = default)
        {
            return await _channel.Reader.ReadAsync(cancellationToken);
        }
    }
}
