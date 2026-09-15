namespace JobAutomation.Infrastructure.HttpExecution;

public class HttpJobExecutorOptions
{
    public const int DefaultMaxResponseBodyBytes = 1_048_576; // 1 MB

    public const int DefaultMaxRedirects = 3;

    public int MaxResponseBodyBytes { get; set; } = DefaultMaxResponseBodyBytes;

    public int MaxRedirects { get; set; } = DefaultMaxRedirects;
}
