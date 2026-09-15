namespace JobAutomation.Domain.Entities;

public class WorkerHeartbeat
{
    public Guid Id { get; set; }

    /// <summary>
    /// Unique identifier for the worker process instance.
    /// </summary>
    public required string WorkerId { get; set; }

    public required string HostName { get; set; }

    public DateTime StartedAtUtc { get; set; }

    public DateTime LastHeartbeatAtUtc { get; set; }

    public DateTime? LastProcessedAtUtc { get; set; }
}
