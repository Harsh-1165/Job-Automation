using JobAutomation.Domain.Enums;

namespace JobAutomation.Application.DTOs.Executions;

public sealed class ExecutionSummaryResponse
{
    public required Guid Id { get; init; }

    public required Guid JobId { get; init; }

    public required ExecutionStatus Status { get; init; }

    public required TriggerType TriggerType { get; init; }

    public required int Attempt { get; init; }

    public int? HttpStatusCode { get; init; }

    public long? DurationMs { get; init; }

    public required DateTime CreatedAtUtc { get; init; }
}
