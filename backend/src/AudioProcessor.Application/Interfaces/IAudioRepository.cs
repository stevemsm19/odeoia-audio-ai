using System;
using System.Threading;
using System.Threading.Tasks;
using AudioProcessor.Domain.Entities;

namespace AudioProcessor.Application.Interfaces
{
    /// <summary>
    /// Provides data access abstractions for managing <see cref="AudioRecord"/> entities in the persistent store.
    /// </summary>
    public interface IAudioRepository
    {
        /// <summary>
        /// Persists a new audio metadata record in the database.
        /// </summary>
        /// <param name="audioRecord">The entity containing the initial upload metadata.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        Task SaveAudioRecordAsync(AudioRecord audioRecord, CancellationToken cancellationToken = default);

        /// <summary>
        /// Retrieves an audio metadata record by its unique identifier.
        /// </summary>
        /// <param name="id">The unique identifier of the audio record.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        /// <returns>The matching <see cref="AudioRecord"/> if found; otherwise, null.</returns>
        Task<AudioRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates the state and metadata details of an existing audio record.
        /// </summary>
        /// <param name="audioRecord">The modified audio record entity containing updated values.</param>
        /// <param name="cancellationToken">A token to monitor for cancellation requests.</param>
        Task UpdateAudioRecordAsync(AudioRecord audioRecord, CancellationToken cancellationToken = default);
    }
}
