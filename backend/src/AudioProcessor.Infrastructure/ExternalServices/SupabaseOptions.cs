namespace AudioProcessor.Infrastructure.ExternalServices
{
    public class SupabaseOptions
    {
        public const string SectionName = "Supabase";

        public string Url { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public string BucketName { get; set; } = string.Empty;
    }
}
