using JobAutomation.Domain.Enums;

namespace JobAutomation.Application.DTOs.Executions;

public sealed class ExecutionResponse
{
    public required Guid Id { get; init; }

    public required Guid JobId { get; init; }

    public required string JobName { get; init; }

    public required ExecutionStatus Status { get; init; }

    public required TriggerType TriggerType { get; init; }

    public required int Attempt { get; init; }

    public required int MaxRetries { get; init; }

    public DateTime? NextRetryAtUtc { get; init; }

    public DateTime? StartedAtUtc { get; init; }

    public DateTime? CompletedAtUtc { get; init; }

    public long? DurationMs { get; init; }

    public int? HttpStatusCode { get; init; }

    public string? ResponseBody { get; init; }

    public string? ErrorMessage { get; init; }

    public required DateTime CreatedAtUtc { get; init; }

    public IReadOnlyList<ExecutionLogResponse> Logs { get; init; } = [];
}
