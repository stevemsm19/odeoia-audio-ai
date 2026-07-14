using AudioProcessor.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AudioProcessor.Infrastructure.Persistence
{
    public class ApplicationDbContext : DbContext
    {
        public DbSet<AudioRecord> AudioRecords { get; set; } = null!;

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<AudioRecord>(entity =>
            {
                entity.ToTable("audio_records");

                entity.HasKey(e => e.Id);

                entity.Property(e => e.Id)
                    .HasColumnName("id")
                    .HasColumnType("uuid")
                    .IsRequired();

                entity.Property(e => e.StorageUrl)
                    .HasColumnName("storage_url")
                    .HasMaxLength(2048)
                    .IsRequired();

                entity.Property(e => e.FileName)
                    .HasColumnName("file_name")
                    .HasMaxLength(512)
                    .IsRequired();

                entity.Property(e => e.CreatedAt)
                    .HasColumnName("created_at")
                    .HasColumnType("timestamptz")
                    .IsRequired();
            });
        }
    }
}
