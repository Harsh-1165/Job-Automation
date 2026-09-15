using JobAutomation.Infrastructure.HttpExecution;

namespace JobAutomation.UnitTests.HttpExecution;

public class HttpJobExecutorRetryAfterTests
{
    [Fact]
    public void ParseRetryAfterSeconds_Delta_ReturnsSeconds()
    {
        var value = HttpJobExecutor.ParseRetryAfterSeconds(
            new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(30)));

        Assert.Equal(30, value);
    }

    [Fact]
    public void ParseRetryAfterSeconds_Null_ReturnsNull()
    {
        Assert.Null(HttpJobExecutor.ParseRetryAfterSeconds(null));
    }
}
