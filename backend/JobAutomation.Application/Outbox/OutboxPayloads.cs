namespace JobAutomation.Application.Outbox;

public sealed class ExecutionEnqueuePayload
{
    public required Guid ExecutionId { get; init; }
}

public sealed class RetryPreparationPayload
{
    public required Guid ExecutionId { get; init; }

    public required DateTime ScheduledForUtc { get; init; }
}
