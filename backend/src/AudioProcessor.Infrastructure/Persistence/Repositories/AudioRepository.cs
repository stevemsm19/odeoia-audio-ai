using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AudioProcessor.Application.Interfaces;
using AudioProcessor.Domain.Entities;
using AudioProcessor.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace AudioProcessor.Infrastructure.Persistence.Repositories
{
    /// <summary>
    /// Implements data access operations using Entity Framework Core.
    /// Acts as the concrete infrastructure adapter for the application's repository interface.
    /// </summary>
    public class AudioRepository : IAudioRepository
    {
        private readonly ApplicationDbContext _dbContext;

        public AudioRepository(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        /// <inheritdoc />
        public async Task SaveAudioRecordAsync(AudioRecord audioRecord, CancellationToken cancellationToken = default)
        {
            await _dbContext.AudioRecords.AddAsync(audioRecord, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        /// <inheritdoc />
        public async Task<AudioRecord?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        {
            return await _dbContext.AudioRecords.FindAsync(new object[] { id }, cancellationToken);
        }

        /// <inheritdoc />
        public async Task UpdateAudioRecordAsync(AudioRecord audioRecord, CancellationToken cancellationToken = default)
        {
            // Records returned by this repository are tracked in the scoped DbContext.
            // Saving detected changes prevents a summary worker from overwriting fields
            // updated concurrently by a compression worker with stale values.
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<AudioRecord>> GetPendingAudioRecordsAsync(CancellationToken cancellationToken = default)
        {
            return await _dbContext.AudioRecords
                .Where(r => r.Status == AudioProcessingStatus.Pending
                    && r.TranscriptionStatus == AudioTranscriptionStatus.Pending
                    && r.SummaryStatus == AudioSummaryStatus.Pending)
                .OrderBy(r => r.CreatedAt)
                .ToListAsync(cancellationToken);
        }
    }
}
