using JobAutomation.Application.Interfaces;

namespace JobAutomation.IntegrationTests.Infrastructure;

public class FakeJobScheduler : IJobScheduler
{
    public Dictionary<Guid, string> ScheduledJobs { get; } = [];

    public HashSet<Guid> RemovedJobs { get; } = [];

    public Task ScheduleRecurringJobAsync(
        Guid jobId,
        string cronExpression,
        CancellationToken cancellationToken = default)
    {
        ScheduledJobs[jobId] = cronExpression;
        RemovedJobs.Remove(jobId);
        return Task.CompletedTask;
    }

    public Task RemoveRecurringJobAsync(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        ScheduledJobs.Remove(jobId);
        RemovedJobs.Add(jobId);
        return Task.CompletedTask;
    }
}
