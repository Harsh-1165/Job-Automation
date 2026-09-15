namespace JobAutomation.Application;

public class OutboxOptions
{
    public const string SectionName = "Outbox";

    public int BatchSize { get; set; } = 100;

    public int PollIntervalSeconds { get; set; } = 5;

    public int LeaseDurationSeconds { get; set; } = 60;

    public int MaxPublishAttempts { get; set; } = 10;

    public int BaseRetryDelaySeconds { get; set; } = 5;

    public int MaxRetryDelaySeconds { get; set; } = 300;

    public int ProcessedRetentionDays { get; set; } = 7;

    public int CleanupBatchSize { get; set; } = 500;

    public int RetryRecoveryBatchSize { get; set; } = 50;
}
