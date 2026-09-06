using AudioProcessor.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AudioProcessor.Infrastructure.Persistence
{
    /// <summary>
    /// Represents the Entity Framework Core database context.
    /// Responsible for configuring entity mappings, table names, and defining the data schema for the domain model.
    /// </summary>
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

                entity.Property(e => e.CompressedStorageUrl)
                    .HasColumnName("compressed_storage_url")
                    .HasMaxLength(2048)
                    .IsRequired(false);

                entity.Property(e => e.Status)
                    .HasColumnName("status")
                    .HasConversion<string>()
                    .HasMaxLength(50)
                    .IsRequired();

                entity.Property(e => e.CompressionDurationMs)
                    .HasColumnName("compression_duration_ms")
                    .HasColumnType("bigint")
                    .IsRequired(false);

                entity.Property(e => e.TranscriptionStatus)
                    .HasColumnName("transcription_status")
                    .HasConversion<string>()
                    .HasMaxLength(50)
                    .IsRequired();

                entity.Property(e => e.TranscriptionDurationMs)
                    .HasColumnName("transcription_duration_ms")
                    .HasColumnType("bigint")
                    .IsRequired(false);

                entity.Property(e => e.TranscriptionError)
                    .HasColumnName("transcription_error")
                    .HasMaxLength(1000)
                    .IsRequired(false);

                entity.Property(e => e.SummaryStorageUrl)
                    .HasColumnName("summary_storage_url")
                    .HasMaxLength(2048)
                    .IsRequired(false);

                entity.Property(e => e.SummaryStatus)
                    .HasColumnName("summary_status")
                    .HasConversion<string>()
                    .HasMaxLength(50)
                    .IsRequired();

                entity.Property(e => e.SummaryDurationMs)
                    .HasColumnName("summary_duration_ms")
                    .HasColumnType("bigint")
                    .IsRequired(false);

                entity.Property(e => e.SummaryError)
                    .HasColumnName("summary_error")
                    .HasMaxLength(1000)
                    .IsRequired(false);

                entity.Property(e => e.UserEmail)
                    .HasColumnName("user_email")
                    .HasMaxLength(320)
                    .IsRequired(false);

                entity.Property(e => e.FilteredStorageUrl)
                    .HasColumnName("filtered_storage_url")
                    .HasMaxLength(2048)
                    .IsRequired(false);

                entity.Property(e => e.NoiseReductionStatus)
                    .HasColumnName("noise_reduction_status")
                    .HasConversion<string>()
                    .HasMaxLength(50)
                    .IsRequired();

                entity.Property(e => e.NoiseReductionDurationMs)
                    .HasColumnName("noise_reduction_duration_ms")
                    .HasColumnType("bigint")
                    .IsRequired(false);

                entity.Property(e => e.NoiseReductionError)
                    .HasColumnName("noise_reduction_error")
                    .HasMaxLength(1000)
                    .IsRequired(false);
            });
        }
    }
}
