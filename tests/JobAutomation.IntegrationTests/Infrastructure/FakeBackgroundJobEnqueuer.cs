using JobAutomation.Application.Interfaces;

namespace JobAutomation.IntegrationTests.Infrastructure;

public class FakeBackgroundJobEnqueuer : IBackgroundJobEnqueuer
{
    public List<Guid> EnqueuedExecutionIds { get; } = [];

    public List<(Guid ExecutionId, TimeSpan Delay)> ScheduledRetries { get; } = [];

    public void EnqueueExecution(Guid executionId)
    {
        EnqueuedExecutionIds.Add(executionId);
    }

    public void ScheduleRetryPreparation(Guid executionId, TimeSpan delay)
    {
        ScheduledRetries.Add((executionId, delay));
    }
}
