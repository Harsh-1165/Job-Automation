namespace JobAutomation.Application.DTOs.Admin;

public sealed class SystemHealthResponse
{
    public required string Status { get; init; }

    public required ComponentHealthResponse Database { get; init; }

    public required ComponentHealthResponse Hangfire { get; init; }

    public required OutboxHealthResponse Outbox { get; init; }

    public required WorkerHealthSummaryResponse Workers { get; init; }
}

public sealed class ComponentHealthResponse
{
    public required string Status { get; init; }
}

public sealed class OutboxHealthResponse
{
    public required string Status { get; init; }

    public int Pending { get; init; }

    public int Processing { get; init; }

    public int Failed { get; init; }

    public double? OldestPendingAgeMinutes { get; init; }
}

public sealed class WorkerHealthSummaryResponse
{
    public required string Status { get; init; }

    public int Total { get; init; }

    public int Healthy { get; init; }

    public int Stale { get; init; }

    public IReadOnlyList<WorkerStatusResponse> Items { get; init; } = [];
}

public sealed class WorkerStatusResponse
{
    public required string WorkerId { get; init; }

    public required string HostName { get; init; }

    public required string Status { get; init; }

    public DateTime LastHeartbeatAtUtc { get; init; }

    public double SecondsSinceHeartbeat { get; init; }

    public DateTime? LastProcessedAtUtc { get; init; }
}
