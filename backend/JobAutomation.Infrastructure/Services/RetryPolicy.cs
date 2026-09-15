using JobAutomation.Application;
using JobAutomation.Application.Interfaces;
using JobAutomation.Domain.Entities;
using Microsoft.Extensions.Options;

namespace JobAutomation.Infrastructure.Services;

public class RetryPolicy : IRetryPolicy
{
    private static readonly HashSet<int> RetryableStatusCodes =
    [
        408, 429, 500, 502, 503, 504
    ];

    private static readonly HashSet<int> NonRetryableStatusCodes =
    [
        400, 401, 403, 404, 405
    ];

    private readonly RetryOptions _options;

    public RetryPolicy(IOptions<RetryOptions> options)
    {
        _options = options.Value;
    }

    public RetryDecision Evaluate(Job job, HttpJobExecutionResult result, int currentAttemptNumber)
    {
        if (result.Succeeded)
        {
            return RetryDecision.NoRetry("Execution succeeded.");
        }

        if (!IsRetryableFailure(result))
        {
            return RetryDecision.NoRetry(GetNonRetryableReason(result));
        }

        if (currentAttemptNumber > job.MaxRetries)
        {
            return RetryDecision.NoRetry("Maximum retry attempts exhausted.");
        }

        var delay = CalculateDelaySeconds(result, currentAttemptNumber);
        return RetryDecision.Schedule(delay, GetRetryableReason(result));
    }

    internal static bool IsRetryableFailure(HttpJobExecutionResult result)
    {
        return result.FailureType switch
        {
            ExecutionFailureType.Timeout => true,
            ExecutionFailureType.Network => true,
            ExecutionFailureType.RequestConstruction => false,
            ExecutionFailureType.HttpResponse => result.HttpStatusCode.HasValue
                && IsRetryableStatusCode(result.HttpStatusCode.Value),
            ExecutionFailureType.Unknown => false,
            _ => false
        };
    }

    internal static bool IsRetryableStatusCode(int statusCode)
    {
        if (NonRetryableStatusCodes.Contains(statusCode))
        {
            return false;
        }

        return RetryableStatusCodes.Contains(statusCode);
    }

    private int CalculateDelaySeconds(HttpJobExecutionResult result, int currentAttemptNumber)
    {
        if (result.HttpStatusCode == 429 && result.RetryAfterSeconds is > 0)
        {
            return ApplyJitter(ClampDelay(result.RetryAfterSeconds.Value));
        }

        var exponential = CalculateExponentialDelay(currentAttemptNumber);
        return ApplyJitter(exponential);
    }

    internal int CalculateExponentialDelay(int currentAttemptNumber)
    {
        var exponent = Math.Min(Math.Max(currentAttemptNumber - 1, 0), 30);
        var multiplier = 1L << exponent;
        var delay = (long)_options.BaseDelaySeconds * multiplier;
        return ClampDelay(delay);
    }

    private int ApplyJitter(int baseDelaySeconds)
    {
        if (_options.JitterPercentage <= 0 || baseDelaySeconds <= 0)
        {
            return baseDelaySeconds;
        }

        var jitterRange = baseDelaySeconds * (_options.JitterPercentage / 100.0);
        var jitter = Random.Shared.NextDouble() * jitterRange;
        return ClampDelay((long)(baseDelaySeconds + jitter));
    }

    private int ClampDelay(long delaySeconds)
    {
        if (delaySeconds < 0)
        {
            return 0;
        }

        if (delaySeconds > _options.MaxBackoffSeconds)
        {
            return _options.MaxBackoffSeconds;
        }

        return (int)delaySeconds;
    }

    private static string GetNonRetryableReason(HttpJobExecutionResult result) => result.FailureType switch
    {
        ExecutionFailureType.RequestConstruction => "Request construction failed.",
        ExecutionFailureType.HttpResponse when result.HttpStatusCode is 401 or 403 =>
            "Authorization failure is not retryable.",
        ExecutionFailureType.HttpResponse => $"HTTP {result.HttpStatusCode} is not retryable.",
        _ => "Failure is not retryable."
    };

    private static string GetRetryableReason(HttpJobExecutionResult result) => result.FailureType switch
    {
        ExecutionFailureType.Timeout => "Request timed out.",
        ExecutionFailureType.Network => "Network connectivity failure.",
        ExecutionFailureType.HttpResponse => $"HTTP {result.HttpStatusCode} is retryable.",
        _ => "Retryable failure."
    };
}
