using Microsoft.Extensions.Logging;

namespace JobAutomation.Infrastructure.BackgroundJobs;

/// <summary>
/// Simple recurring job to verify Hangfire worker connectivity.
/// Full job execution logic will be implemented in a later phase.
/// </summary>
public class WorkerHealthJob
{
    private readonly ILogger<WorkerHealthJob> _logger;

    public WorkerHealthJob(ILogger<WorkerHealthJob> logger)
    {
        _logger = logger;
    }

    public Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Worker health check executed at {TimestampUtc}",
            DateTime.UtcNow);

        return Task.CompletedTask;
    }
}
