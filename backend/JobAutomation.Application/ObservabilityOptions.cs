namespace JobAutomation.Application;

public class ObservabilityOptions
{
    public const string SectionName = "Observability";

    public int WorkerHeartbeatIntervalSeconds { get; set; } = 20;

    public int WorkerStaleThresholdSeconds { get; set; } = 60;

    public int OutboxPendingDegradedThreshold { get; set; } = 100;

    public int OutboxPendingUnhealthyThreshold { get; set; } = 500;

    public int OutboxOldestPendingDegradedMinutes { get; set; } = 5;

    public int OutboxOldestPendingUnhealthyMinutes { get; set; } = 30;
}
