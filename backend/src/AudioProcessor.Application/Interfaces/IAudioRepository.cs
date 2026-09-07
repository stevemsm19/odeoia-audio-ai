using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AudioProcessor.Domain.Entities;

namespace AudioProcessor.Application.Interfaces
{
    public interface IAudioRepository
    {
        Task SaveAudioRecordAsync(AudioRecord audioRecord, CancellationToken cancellationToken = default);

        Task<AudioRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

        Task UpdateAudioRecordAsync(AudioRecord audioRecord, CancellationToken cancellationToken = default);

        // Audios not yet claimed by the background processing loop, oldest first.
        Task<IReadOnlyList<AudioRecord>> GetPendingAudioRecordsAsync(CancellationToken cancellationToken = default);

        // userEmail must already be normalized (trimmed, lowercased).
        Task<IReadOnlyList<AudioRecord>> GetByUserEmailAsync(string userEmail, CancellationToken cancellationToken = default);
    }
}
