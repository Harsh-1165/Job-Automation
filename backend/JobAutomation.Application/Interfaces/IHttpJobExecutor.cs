using JobAutomation.Application;
using JobAutomation.Domain.Entities;

namespace JobAutomation.Application.Interfaces;

public interface IHttpJobExecutor
{
    Task<HttpJobExecutionResult> ExecuteAsync(Job job, CancellationToken cancellationToken = default);
}

public sealed class HttpJobExecutionResult
{
    public bool Succeeded { get; init; }

    public int? HttpStatusCode { get; init; }

    public string? ResponseBody { get; init; }

    public string? ErrorMessage { get; init; }

    public ExecutionFailureType FailureType { get; init; } = ExecutionFailureType.None;

    /// <summary>
    /// Retry-After hint in seconds from a 429 response, when present and valid.
    /// </summary>
    public int? RetryAfterSeconds { get; init; }
}
