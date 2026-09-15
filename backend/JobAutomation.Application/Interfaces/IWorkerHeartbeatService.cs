namespace JobAutomation.Application.Interfaces;

public interface IWorkerHeartbeatService
{
    Task RecordHeartbeatAsync(CancellationToken cancellationToken = default);

    Task RecordProcessingAsync(CancellationToken cancellationToken = default);
}
