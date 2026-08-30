using System;
using System.Collections.Generic;
using System.Linq;
using AudioProcessor.Application.Interfaces;
using AudioProcessor.Domain.Entities;
using AudioProcessor.Domain.Enums;

namespace AudioProcessor.Infrastructure.Persistence.Repositories
{
    public class AudioRepository : IAudioRepository
    {
        private readonly ApplicationDbContext _dbContext;

        public AudioRepository(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public void SaveAudioRecord(AudioRecord audioRecord)
        {
            _dbContext.AudioRecords.Add(audioRecord);
            _dbContext.SaveChanges();
        }

        public AudioRecord? GetById(Guid id)
        {
            return _dbContext.AudioRecords.Find(id);
        }

        public void UpdateAudioRecord(AudioRecord audioRecord)
        {
            _dbContext.SaveChanges();
        }

        public IReadOnlyList<AudioRecord> GetPendingAudioRecords()
        {
            return _dbContext.AudioRecords
                .Where(r => r.Status == AudioProcessingStatus.Pending
                    && r.TranscriptionStatus == AudioTranscriptionStatus.Pending
                    && r.SummaryStatus == AudioSummaryStatus.Pending)
                .OrderBy(r => r.CreatedAt)
                .ToList();
        }

        public IReadOnlyList<AudioRecord> GetByUserEmail(string userEmail)
        {
            return _dbContext.AudioRecords
                .Where(r => r.UserEmail == userEmail)
                .OrderByDescending(r => r.CreatedAt)
                .ToList();
        }
    }
}
