using System;
using AudioProcessor.Domain.Entities;
using AudioProcessor.Domain.Enums;
using Xunit;

namespace AudioProcessor.Tests.Domain
{
    public class AudioRecordTests
    {
        private static AudioRecord NewRecord() => new(
            id: Guid.NewGuid(),
            storageUrl: "https://example.supabase.co/audio-files/original.mp3",
            fileName: "original.mp3",
            createdAt: DateTime.UtcNow);

        [Fact]
        public void NewRecord_AllStagesArePendingAndOverallStatusIsPending()
        {
            var record = NewRecord();

            Assert.Equal(AudioProcessingStatus.Pending, record.Status);
            Assert.Equal(AudioTranscriptionStatus.Pending, record.TranscriptionStatus);
            Assert.Equal(AudioSummaryStatus.Pending, record.SummaryStatus);
            Assert.Equal(AudioOverallStatus.Pending, record.OverallStatus);
        }

        [Fact]
        public void MarkTranscriptionAsProcessing_SetsStatusAndClearsError()
        {
            var record = NewRecord();

            record.MarkTranscriptionAsProcessing();

            Assert.Equal(AudioTranscriptionStatus.Processing, record.TranscriptionStatus);
            Assert.Null(record.TranscriptionError);
        }

        [Fact]
        public void MarkTranscriptionAsCompleted_SetsStatusAndDuration()
        {
            var record = NewRecord();
            record.MarkTranscriptionAsProcessing();

            record.MarkTranscriptionAsCompleted(1234);

            Assert.Equal(AudioTranscriptionStatus.Completed, record.TranscriptionStatus);
            Assert.Equal(1234, record.TranscriptionDurationMs);
            Assert.Null(record.TranscriptionError);
        }

        [Fact]
        public void MarkTranscriptionAsFailed_SetsStatusAndError()
        {
            var record = NewRecord();

            record.MarkTranscriptionAsFailed("whisper-cli exited with 1", 500);

            Assert.Equal(AudioTranscriptionStatus.Failed, record.TranscriptionStatus);
            Assert.Equal("whisper-cli exited with 1", record.TranscriptionError);
            Assert.Equal(500, record.TranscriptionDurationMs);
        }

        [Fact]
        public void MarkSummaryAsCompleted_StoresStorageUrlWithoutTruncation()
        {
            var record = NewRecord();
            string longUrl = "https://example.supabase.co/summaries/" + new string('a', 600) + ".txt";

            record.MarkSummaryAsCompleted(longUrl, 42);

            // No length limit on the summary — it's a Storage URL now, not inline
            // text, so nothing should truncate it.
            Assert.Equal(longUrl, record.SummaryStorageUrl);
            Assert.Equal(AudioSummaryStatus.Completed, record.SummaryStatus);
        }

        [Fact]
        public void MarkSummaryAsCompleted_WithEmptyUrl_Throws()
        {
            var record = NewRecord();

            Assert.Throws<ArgumentException>(() => record.MarkSummaryAsCompleted("", 10));
        }

        [Theory]
        [InlineData(AudioProcessingStatus.Pending, AudioTranscriptionStatus.Pending, AudioSummaryStatus.Pending, AudioOverallStatus.Pending)]
        [InlineData(AudioProcessingStatus.Processing, AudioTranscriptionStatus.Pending, AudioSummaryStatus.Pending, AudioOverallStatus.Processing)]
        [InlineData(AudioProcessingStatus.Completed, AudioTranscriptionStatus.Processing, AudioSummaryStatus.Pending, AudioOverallStatus.Processing)]
        [InlineData(AudioProcessingStatus.Completed, AudioTranscriptionStatus.Completed, AudioSummaryStatus.Completed, AudioOverallStatus.Completed)]
        [InlineData(AudioProcessingStatus.Failed, AudioTranscriptionStatus.Failed, AudioSummaryStatus.Failed, AudioOverallStatus.Completed)]
        [InlineData(AudioProcessingStatus.Completed, AudioTranscriptionStatus.Failed, AudioSummaryStatus.Failed, AudioOverallStatus.Completed)]
        [InlineData(AudioProcessingStatus.Processing, AudioTranscriptionStatus.Completed, AudioSummaryStatus.Completed, AudioOverallStatus.Processing)]
        public void OverallStatus_IsComputedFromPerStageTerminalState(
            AudioProcessingStatus compression,
            AudioTranscriptionStatus transcription,
            AudioSummaryStatus summary,
            AudioOverallStatus expectedOverall)
        {
            var record = NewRecord();
            DriveCompressionTo(record, compression);
            DriveTranscriptionTo(record, transcription);
            DriveSummaryTo(record, summary);

            // "Completed" means every stage reached a terminal state — it is not a
            // success verdict. A run where every stage failed is still, correctly,
            // an overall status of Completed.
            Assert.Equal(expectedOverall, record.OverallStatus);
        }

        private static void DriveCompressionTo(AudioRecord record, AudioProcessingStatus target)
        {
            switch (target)
            {
                case AudioProcessingStatus.Pending:
                    return;
                case AudioProcessingStatus.Processing:
                    record.MarkAsProcessing();
                    return;
                case AudioProcessingStatus.Completed:
                    record.MarkAsCompleted("https://example.supabase.co/audio-files/original.aac", 10);
                    return;
                case AudioProcessingStatus.Failed:
                    record.MarkAsFailed(10);
                    return;
            }
        }

        private static void DriveTranscriptionTo(AudioRecord record, AudioTranscriptionStatus target)
        {
            switch (target)
            {
                case AudioTranscriptionStatus.Pending:
                    return;
                case AudioTranscriptionStatus.Processing:
                    record.MarkTranscriptionAsProcessing();
                    return;
                case AudioTranscriptionStatus.Completed:
                    record.MarkTranscriptionAsCompleted(10);
                    return;
                case AudioTranscriptionStatus.Failed:
                    record.MarkTranscriptionAsFailed("error", 10);
                    return;
            }
        }

        private static void DriveSummaryTo(AudioRecord record, AudioSummaryStatus target)
        {
            switch (target)
            {
                case AudioSummaryStatus.Pending:
                    return;
                case AudioSummaryStatus.Processing:
                    record.MarkSummaryAsProcessing();
                    return;
                case AudioSummaryStatus.Completed:
                    record.MarkSummaryAsCompleted("https://example.supabase.co/summaries/summary.txt", 10);
                    return;
                case AudioSummaryStatus.Failed:
                    record.MarkSummaryAsFailed("error", 10);
                    return;
            }
        }
    }
}
