namespace JobAutomation.Application.DTOs.Executions;

public sealed class ExecutionLogResponse
{
    public required Guid Id { get; init; }

    public required string Level { get; init; }

    public required string Message { get; init; }

    public required DateTime CreatedAtUtc { get; init; }
}
