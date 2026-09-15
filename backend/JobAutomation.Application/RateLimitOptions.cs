namespace JobAutomation.Application;

public class RateLimitOptions
{
    public const string SectionName = "RateLimiting";

    public int AuthPermitLimit { get; set; } = 10;

    public int AuthWindowSeconds { get; set; } = 60;

    public int RunPermitLimit { get; set; } = 30;

    public int RunWindowSeconds { get; set; } = 60;

    public int RetryPermitLimit { get; set; } = 20;

    public int RetryWindowSeconds { get; set; } = 60;

    /// <summary>
    /// When true, rate limiting applies even in the Testing environment (for integration tests).
    /// </summary>
    public bool EnableInTesting { get; set; }
}
