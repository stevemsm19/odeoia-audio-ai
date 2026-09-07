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
    // EF Core adapter for IAudioRepository.
    public class AudioRepository : IAudioRepository
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IAudioStatusNotifier _statusNotifier;

        public AudioRepository(ApplicationDbContext dbContext, IAudioStatusNotifier statusNotifier)
        {
            _dbContext = dbContext;
            _statusNotifier = statusNotifier;
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
            // Saves tracked changes only — avoids a concurrent worker overwriting fields with stale values.
            await _dbContext.SaveChangesAsync(cancellationToken);
            await _statusNotifier.NotifyStatusChangedAsync(audioRecord.Id, cancellationToken);
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

        /// <inheritdoc />
        public async Task<IReadOnlyList<AudioRecord>> GetByUserEmailAsync(string userEmail, CancellationToken cancellationToken = default)
        {
            return await _dbContext.AudioRecords
                .Where(r => r.UserEmail == userEmail)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync(cancellationToken);
        }
    }
}
