using JobAutomation.Application;
using JobAutomation.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace JobAutomation.Infrastructure.BackgroundJobs;

public class ScheduledJobBackgroundTask
{
    private readonly IExecutionService _executionService;
    private readonly ISystemClock _clock;
    private readonly ILogger<ScheduledJobBackgroundTask> _logger;

    public ScheduledJobBackgroundTask(
        IExecutionService executionService,
        ISystemClock clock,
        ILogger<ScheduledJobBackgroundTask> logger)
    {
        _executionService = executionService;
        _clock = clock;
        _logger = logger;
    }

    public async Task ExecuteAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var occurrenceUtc = ScheduledOccurrenceKeys.TruncateToMinute(_clock.UtcNow);

        _logger.LogInformation(
            "ScheduledJobTriggered JobId={JobId} OccurrenceUtc={OccurrenceUtc}",
            jobId,
            occurrenceUtc);

        await _executionService.CreateScheduledExecutionAsync(jobId, occurrenceUtc, cancellationToken);
    }
}
