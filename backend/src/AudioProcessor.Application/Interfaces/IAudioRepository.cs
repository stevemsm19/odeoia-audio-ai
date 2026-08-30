using System;
using System.Collections.Generic;
using AudioProcessor.Domain.Entities;

namespace AudioProcessor.Application.Interfaces
{
    public interface IAudioRepository
    {
        void SaveAudioRecord(AudioRecord audioRecord);
        AudioRecord? GetById(Guid id);
        void UpdateAudioRecord(AudioRecord audioRecord);

        IReadOnlyList<AudioRecord> GetPendingAudioRecords();
        IReadOnlyList<AudioRecord> GetByUserEmail(string userEmail);
    }
}
