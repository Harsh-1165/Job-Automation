namespace JobAutomation.Application.Interfaces;

public interface IOutboxDispatcher
{
    Task<int> DispatchBatchAsync(CancellationToken cancellationToken = default);

    Task<int> CleanupProcessedAsync(CancellationToken cancellationToken = default);
}
