using System;
using System.Threading;
using System.Threading.Tasks;
using AudioProcessor.Application.Models;

namespace AudioProcessor.Application.Interfaces
{
    /// <summary>
    /// Represents an in-memory queue to schedule and process audio compression tasks asynchronously.
    /// Acts as a buffer between the HTTP API controllers and the background workers.
    /// </summary>
    public interface IBackgroundJobQueue
    {
        /// <summary>
        /// Enqueues the identifier of a record for background processing.
        /// </summary>
        /// <param name="audioRecordId">The unique identifier of the audio record to process.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        ValueTask EnqueueJobAsync(AudioProcessingJob job, CancellationToken cancellationToken = default);

        /// <summary>
        /// Dequeues an identifier from the queue, blocking until a job becomes available.
        /// </summary>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>A <see cref="ValueTask{Guid}"/> containing the dequeued audio record identifier.</returns>
        ValueTask<AudioProcessingJob> DequeueJobAsync(CancellationToken cancellationToken = default);
    }
}
