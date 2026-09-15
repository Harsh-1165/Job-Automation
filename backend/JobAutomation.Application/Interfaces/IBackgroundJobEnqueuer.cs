namespace JobAutomation.Application.Interfaces;

public interface IBackgroundJobEnqueuer
{
    void EnqueueExecution(Guid executionId);

    void ScheduleRetryPreparation(Guid executionId, TimeSpan delay);
}
