using JobAutomation.Domain.Enums;

namespace JobAutomation.Application.DTOs.Dashboard;

public sealed class DashboardSummaryResponse
{
    public required string Range { get; init; }

    public required JobMetricsResponse Jobs { get; init; }

    public required ExecutionMetricsResponse Executions { get; init; }

    public required RateMetricsResponse Rates { get; init; }

    public required DurationMetricsResponse Duration { get; init; }

    public required IReadOnlyList<RecentExecutionResponse> RecentExecutions { get; init; }

    public required IReadOnlyList<RecentFailureResponse> RecentFailures { get; init; }

    public required RetryMetricsResponse Retries { get; init; }
}

public sealed class JobMetricsResponse
{
    public int Total { get; init; }

    public int Active { get; init; }

    public int Paused { get; init; }

    public int Archived { get; init; }
}

public sealed class ExecutionMetricsResponse
{
    public int Total { get; init; }

    public int Queued { get; init; }

    public int Running { get; init; }

    public int Retrying { get; init; }

    public int Succeeded { get; init; }

    public int Failed { get; init; }

    public int Cancelled { get; init; }
}

public sealed class RateMetricsResponse
{
    /// <summary>
    /// Succeeded / (Succeeded + Failed). Cancelled executions are excluded from the denominator.
    /// </summary>
    public double? SuccessRate { get; init; }

    /// <summary>
    /// Failed / (Succeeded + Failed). Cancelled executions are excluded.
    /// </summary>
    public double? FailureRate { get; init; }

    public int CompletedCount { get; init; }
}

public sealed class DurationMetricsResponse
{
    public double? AverageMs { get; init; }

    public double? MinMs { get; init; }

    public double? MaxMs { get; init; }
}

public sealed class RecentExecutionResponse
{
    public required Guid Id { get; init; }

    public required Guid JobId { get; init; }

    public required string JobName { get; init; }

    public required ExecutionStatus Status { get; init; }

    public required TriggerType TriggerType { get; init; }

    public int Attempt { get; init; }

    public DateTime CreatedAtUtc { get; init; }

    public DateTime? CompletedAtUtc { get; init; }

    public long? DurationMs { get; init; }
}

public sealed class RecentFailureResponse
{
    public required Guid Id { get; init; }

    public required Guid JobId { get; init; }

    public required string JobName { get; init; }

    public int? HttpStatusCode { get; init; }

    public string? ErrorMessage { get; init; }

    public int Attempt { get; init; }

    public DateTime CompletedAtUtc { get; init; }
}

public sealed class RetryMetricsResponse
{
    public int CurrentlyRetrying { get; init; }

    public int RetriesInRange { get; init; }

    public int SucceededAfterRetryInRange { get; init; }
}
