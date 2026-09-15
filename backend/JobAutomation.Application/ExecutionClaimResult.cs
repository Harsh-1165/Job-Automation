namespace JobAutomation.Application;

public enum ExecutionClaimOutcome
{
    Claimed,
    AlreadyClaimed,
    NotFound,
    NotExecutable
}

public sealed class ExecutionClaimResult
{
    public required ExecutionClaimOutcome Outcome { get; init; }

    public Guid? LeaseId { get; init; }
}
