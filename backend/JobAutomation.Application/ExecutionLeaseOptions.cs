namespace JobAutomation.Application;

public class ExecutionLeaseOptions
{
    public const string SectionName = "ExecutionLease";

    public int LeaseDurationSeconds { get; set; } = 60;

    public int HeartbeatIntervalSeconds { get; set; } = 20;

    public int RecoveryBatchSize { get; set; } = 100;
}
