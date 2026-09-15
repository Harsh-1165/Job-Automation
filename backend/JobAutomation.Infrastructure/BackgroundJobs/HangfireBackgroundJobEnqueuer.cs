using Hangfire;
using JobAutomation.Application.Interfaces;

namespace JobAutomation.Infrastructure.BackgroundJobs;

public class HangfireBackgroundJobEnqueuer : IBackgroundJobEnqueuer
{
    public void EnqueueExecution(Guid executionId)
    {
        BackgroundJob.Enqueue<ExecuteJobBackgroundTask>(
            task => task.ExecuteAsync(executionId, CancellationToken.None));
    }

    public void ScheduleRetryPreparation(Guid executionId, TimeSpan delay)
    {
        BackgroundJob.Schedule<ExecuteJobBackgroundTask>(
            task => task.PrepareRetryAsync(executionId, CancellationToken.None),
            delay);
    }
}
