namespace JobAutomation.Application.DTOs.Executions;

public sealed class ScheduledExecutionResult
{
    public required Guid ExecutionId { get; init; }

    public required bool WasCreated { get; init; }
}
