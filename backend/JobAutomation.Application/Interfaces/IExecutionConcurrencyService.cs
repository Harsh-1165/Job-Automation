using JobAutomation.Application;

namespace JobAutomation.Application.Interfaces;

public interface IExecutionConcurrencyService
{
    Task<ExecutionClaimResult> TryClaimAsync(Guid executionId, CancellationToken cancellationToken = default);

    Task<bool> TryHeartbeatAsync(Guid executionId, Guid leaseId, CancellationToken cancellationToken = default);

    Task<bool> TryCompleteSucceededAsync(
        Guid executionId,
        Guid leaseId,
        int? httpStatusCode,
        string? responseBody,
        CancellationToken cancellationToken = default);

    Task<bool> TryCompleteFailedAsync(
        Guid executionId,
        Guid leaseId,
        int? httpStatusCode,
        string? responseBody,
        string? errorMessage,
        CancellationToken cancellationToken = default);

    Task<bool> TryScheduleRetryAsync(
        Guid executionId,
        Guid leaseId,
        int? httpStatusCode,
        string? responseBody,
        string? errorMessage,
        DateTime nextRetryAtUtc,
        CancellationToken cancellationToken = default);

    Task<bool> TryPrepareRetryForProcessingAsync(
        Guid executionId,
        CancellationToken cancellationToken = default);

    Task<bool> TryCancelRetryAsync(
        Guid executionId,
        string errorMessage,
        CancellationToken cancellationToken = default);

    Task<bool> TryCancelQueuedAsync(Guid executionId, CancellationToken cancellationToken = default);

    Task<bool> TryCancelRetryingAsync(Guid executionId, CancellationToken cancellationToken = default);

    Task<bool> TryManualRetryAsync(
        Guid executionId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically recover a stale Running execution back to Queued. Returns true if recovered.
    /// </summary>
    Task<bool> TryRecoverStaleAsync(Guid executionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> FindStaleExecutionIdsAsync(int batchSize, CancellationToken cancellationToken = default);
}
