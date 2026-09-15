namespace JobAutomation.Infrastructure.Worker;

public sealed class WorkerIdentity
{
    public string WorkerId { get; } = $"worker-{Environment.MachineName}-{Guid.NewGuid():N}";

    public string HostName { get; } = Environment.MachineName;

    public DateTime StartedAtUtc { get; } = DateTime.UtcNow;
}
