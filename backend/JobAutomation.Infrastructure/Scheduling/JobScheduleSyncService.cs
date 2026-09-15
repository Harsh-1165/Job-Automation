using JobAutomation.Application.Interfaces;
using JobAutomation.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JobAutomation.Infrastructure.Scheduling;

public class JobScheduleSyncService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IJobScheduler _jobScheduler;
    private readonly ICronScheduleValidator _cronValidator;
    private readonly ILogger<JobScheduleSyncService> _logger;

    public JobScheduleSyncService(
        IApplicationDbContext dbContext,
        IJobScheduler jobScheduler,
        ICronScheduleValidator cronValidator,
        ILogger<JobScheduleSyncService> logger)
    {
        _dbContext = dbContext;
        _jobScheduler = jobScheduler;
        _cronValidator = cronValidator;
        _logger = logger;
    }

    public async Task SyncActiveJobSchedulesAsync(CancellationToken cancellationToken = default)
    {
        var jobs = await _dbContext.Jobs
            .Where(j => j.Status == JobStatus.Active
                        && j.CronExpression != null
                        && j.CronExpression != "")
            .ToListAsync(cancellationToken);

        foreach (var job in jobs)
        {
            try
            {
                await _jobScheduler.ScheduleRecurringJobAsync(job.Id, job.CronExpression!, cancellationToken);
                job.NextRunAtUtc = _cronValidator.GetNextOccurrenceUtc(job.CronExpression, DateTime.UtcNow);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to register recurring schedule for job {JobId} during startup sync",
                    job.Id);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Synced {Count} active job schedules", jobs.Count);
    }
}
