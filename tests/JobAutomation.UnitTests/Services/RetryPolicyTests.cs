using JobAutomation.Application;
using JobAutomation.Application.Interfaces;
using JobAutomation.Domain.Entities;
using JobAutomation.Domain.Enums;
using JobAutomation.Infrastructure.Services;
using Microsoft.Extensions.Options;

namespace JobAutomation.UnitTests.Services;

public class RetryPolicyTests
{
    private static RetryPolicy CreatePolicy(int jitterPercentage = 0) =>
        new(Options.Create(new RetryOptions
        {
            BaseDelaySeconds = 5,
            MaxBackoffSeconds = 300,
            JitterPercentage = jitterPercentage
        }));

    private static Job CreateJob(int maxRetries = 3) => new()
    {
        Id = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        Name = "Test",
        HttpMethod = "GET",
        TargetUrl = "https://example.com",
        Status = JobStatus.Active,
        MaxRetries = maxRetries,
        TimeoutSeconds = 30,
        CreatedAtUtc = DateTime.UtcNow,
        UpdatedAtUtc = DateTime.UtcNow
    };

    [Theory]
    [InlineData(200)]
    [InlineData(201)]
    public void Evaluate_Success_DoesNotRetry(int statusCode)
    {
        var policy = CreatePolicy();
        var decision = policy.Evaluate(
            CreateJob(),
            new HttpJobExecutionResult
            {
                Succeeded = true,
                HttpStatusCode = statusCode
            },
            1);

        Assert.False(decision.ShouldRetry);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    [InlineData(405)]
    public void Evaluate_ClientErrors_DoNotRetry(int statusCode)
    {
        var policy = CreatePolicy();
        var decision = policy.Evaluate(
            CreateJob(),
            new HttpJobExecutionResult
            {
                Succeeded = false,
                HttpStatusCode = statusCode,
                FailureType = ExecutionFailureType.HttpResponse
            },
            1);

        Assert.False(decision.ShouldRetry);
    }

    [Theory]
    [InlineData(408)]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(502)]
    [InlineData(503)]
    [InlineData(504)]
    public void Evaluate_RetryableHttpStatuses_ScheduleRetry(int statusCode)
    {
        var policy = CreatePolicy();
        var decision = policy.Evaluate(
            CreateJob(),
            new HttpJobExecutionResult
            {
                Succeeded = false,
                HttpStatusCode = statusCode,
                FailureType = ExecutionFailureType.HttpResponse
            },
            1);

        Assert.True(decision.ShouldRetry);
        Assert.InRange(decision.DelaySeconds, 5, 300);
    }

    [Fact]
    public void Evaluate_Timeout_Retries()
    {
        var policy = CreatePolicy();
        var decision = policy.Evaluate(
            CreateJob(),
            new HttpJobExecutionResult
            {
                Succeeded = false,
                FailureType = ExecutionFailureType.Timeout
            },
            1);

        Assert.True(decision.ShouldRetry);
    }

    [Fact]
    public void Evaluate_NetworkFailure_Retries()
    {
        var policy = CreatePolicy();
        var decision = policy.Evaluate(
            CreateJob(),
            new HttpJobExecutionResult
            {
                Succeeded = false,
                FailureType = ExecutionFailureType.Network
            },
            1);

        Assert.True(decision.ShouldRetry);
    }

    [Fact]
    public void Evaluate_RequestConstruction_DoesNotRetry()
    {
        var policy = CreatePolicy();
        var decision = policy.Evaluate(
            CreateJob(),
            new HttpJobExecutionResult
            {
                Succeeded = false,
                FailureType = ExecutionFailureType.RequestConstruction
            },
            1);

        Assert.False(decision.ShouldRetry);
    }

    [Fact]
    public void Evaluate_MaxRetriesZero_DoesNotRetry()
    {
        var policy = CreatePolicy();
        var decision = policy.Evaluate(
            CreateJob(maxRetries: 0),
            new HttpJobExecutionResult
            {
                Succeeded = false,
                HttpStatusCode = 500,
                FailureType = ExecutionFailureType.HttpResponse
            },
            1);

        Assert.False(decision.ShouldRetry);
    }

    [Fact]
    public void Evaluate_RetriesExhausted_DoesNotRetry()
    {
        var policy = CreatePolicy();
        var decision = policy.Evaluate(
            CreateJob(maxRetries: 2),
            new HttpJobExecutionResult
            {
                Succeeded = false,
                HttpStatusCode = 503,
                FailureType = ExecutionFailureType.HttpResponse
            },
            3);

        Assert.False(decision.ShouldRetry);
        Assert.Contains("exhausted", decision.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CalculateExponentialDelay_FollowsBackoff()
    {
        var policy = CreatePolicy();

        Assert.Equal(5, policy.CalculateExponentialDelay(1));
        Assert.Equal(10, policy.CalculateExponentialDelay(2));
        Assert.Equal(20, policy.CalculateExponentialDelay(3));
        Assert.Equal(40, policy.CalculateExponentialDelay(4));
    }

    [Fact]
    public void CalculateExponentialDelay_RespectsMaxBackoff()
    {
        var policy = CreatePolicy();
        Assert.Equal(300, policy.CalculateExponentialDelay(20));
    }

    [Fact]
    public void Evaluate_429WithRetryAfter_UsesRetryAfter()
    {
        var policy = CreatePolicy();
        var decision = policy.Evaluate(
            CreateJob(),
            new HttpJobExecutionResult
            {
                Succeeded = false,
                HttpStatusCode = 429,
                FailureType = ExecutionFailureType.HttpResponse,
                RetryAfterSeconds = 30
            },
            1);

        Assert.True(decision.ShouldRetry);
        Assert.Equal(30, decision.DelaySeconds);
    }

    [Fact]
    public void Evaluate_429WithExcessiveRetryAfter_IsClamped()
    {
        var policy = CreatePolicy();
        var decision = policy.Evaluate(
            CreateJob(),
            new HttpJobExecutionResult
            {
                Succeeded = false,
                HttpStatusCode = 429,
                FailureType = ExecutionFailureType.HttpResponse,
                RetryAfterSeconds = 9999
            },
            1);

        Assert.True(decision.ShouldRetry);
        Assert.Equal(300, decision.DelaySeconds);
    }

    [Fact]
    public void Evaluate_429WithoutRetryAfter_FallsBackToExponential()
    {
        var policy = CreatePolicy();
        var decision = policy.Evaluate(
            CreateJob(),
            new HttpJobExecutionResult
            {
                Succeeded = false,
                HttpStatusCode = 429,
                FailureType = ExecutionFailureType.HttpResponse
            },
            2);

        Assert.True(decision.ShouldRetry);
        Assert.Equal(10, decision.DelaySeconds);
    }
}
