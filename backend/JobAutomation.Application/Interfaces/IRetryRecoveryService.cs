namespace JobAutomation.Application.Interfaces;

public interface IRetryRecoveryService
{
    Task<int> RecoverOrphanedRetriesAsync(CancellationToken cancellationToken = default);
}
