namespace JobAutomation.Application;

public sealed class RetryDecision
{
    public required bool ShouldRetry { get; init; }

    public int DelaySeconds { get; init; }

    public required string Reason { get; init; }

    public static RetryDecision NoRetry(string reason) => new()
    {
        ShouldRetry = false,
        Reason = reason
    };

    public static RetryDecision Schedule(int delaySeconds, string reason) => new()
    {
        ShouldRetry = true,
        DelaySeconds = delaySeconds,
        Reason = reason
    };
}
