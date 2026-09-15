namespace JobAutomation.Application.Interfaces;

public interface IJobScheduler
{
    Task ScheduleRecurringJobAsync(
        Guid jobId,
        string cronExpression,
        CancellationToken cancellationToken = default);

    Task RemoveRecurringJobAsync(
        Guid jobId,
        CancellationToken cancellationToken = default);
}
