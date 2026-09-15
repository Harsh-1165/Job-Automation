namespace JobAutomation.Application;

public class RetryOptions
{
    public const string SectionName = "Retry";

    public int BaseDelaySeconds { get; set; } = 5;

    public int MaxBackoffSeconds { get; set; } = 300;

    public int JitterPercentage { get; set; } = 25;
}
