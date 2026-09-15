using Hangfire;
using JobAutomation.Application;
using JobAutomation.Application.Interfaces;
using JobAutomation.Infrastructure.BackgroundJobs;
using Microsoft.Extensions.Logging;

namespace JobAutomation.Infrastructure.Scheduling;

public class HangfireJobScheduler : IJobScheduler
{
    private readonly IRecurringJobManager _recurringJobManager;
    private readonly ILogger<HangfireJobScheduler> _logger;

    public HangfireJobScheduler(
        IRecurringJobManager recurringJobManager,
        ILogger<HangfireJobScheduler> logger)
    {
        _recurringJobManager = recurringJobManager;
        _logger = logger;
    }

    public Task ScheduleRecurringJobAsync(
        Guid jobId,
        string cronExpression,
        CancellationToken cancellationToken = default)
    {
        var recurringJobId = RecurringJobIds.ForJob(jobId);

        _recurringJobManager.AddOrUpdate<ScheduledJobBackgroundTask>(
            recurringJobId,
            task => task.ExecuteAsync(jobId, CancellationToken.None),
            cronExpression);

        _logger.LogInformation(
            "RecurringScheduleRegistered JobId={JobId} RecurringJobId={RecurringJobId}",
            jobId,
            recurringJobId);

        return Task.CompletedTask;
    }

    public Task RemoveRecurringJobAsync(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        var recurringJobId = RecurringJobIds.ForJob(jobId);
        _recurringJobManager.RemoveIfExists(recurringJobId);

        _logger.LogInformation(
            "RecurringScheduleRemoved JobId={JobId} RecurringJobId={RecurringJobId}",
            jobId,
            recurringJobId);

        return Task.CompletedTask;
    }
}
