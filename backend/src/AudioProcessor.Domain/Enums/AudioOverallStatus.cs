namespace AudioProcessor.Domain.Enums
{
    /// <summary>
    /// Computed, never stored. "Completed" means every stage reached a terminal
    /// state — it is not a success verdict; per-stage statuses are the source of truth.
    /// </summary>
    public enum AudioOverallStatus
    {
        Pending = 0,
        Processing = 1,
        Completed = 2
    }
}
