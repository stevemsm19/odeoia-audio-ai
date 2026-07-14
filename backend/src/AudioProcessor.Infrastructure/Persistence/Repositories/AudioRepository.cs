using System.Threading;
using System.Threading.Tasks;
using AudioProcessor.Application.Interfaces;
using AudioProcessor.Domain.Entities;

namespace AudioProcessor.Infrastructure.Persistence.Repositories
{
    public class AudioRepository : IAudioRepository
    {
        private readonly ApplicationDbContext _dbContext;

        public AudioRepository(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task SaveAudioRecordAsync(AudioRecord audioRecord, CancellationToken cancellationToken = default)
        {
            await _dbContext.AudioRecords.AddAsync(audioRecord, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
