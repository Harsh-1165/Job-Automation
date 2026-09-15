using JobAutomation.Application.DTOs.Executions;

namespace JobAutomation.Application.Interfaces;

public interface IExecutionService
{
    Task<RunJobResponse> QueueManualExecutionAsync(
        Guid jobId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<ScheduledExecutionResult> CreateScheduledExecutionAsync(
        Guid jobId,
        DateTime occurrenceUtc,
        CancellationToken cancellationToken = default);

    Task ProcessExecutionAsync(Guid executionId, CancellationToken cancellationToken = default);

    Task PrepareRetryAsync(Guid executionId, CancellationToken cancellationToken = default);

    Task RecoverStaleExecutionsAsync(CancellationToken cancellationToken = default);

    Task<ExecutionResponse> GetByIdAsync(Guid executionId, CancellationToken cancellationToken = default);

    Task<PagedExecutionsResponse> ListByJobIdAsync(
        Guid jobId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<RunJobResponse> RetryExecutionAsync(
        Guid executionId,
        string idempotencyKey,
        CancellationToken cancellationToken = default);

    Task<ExecutionResponse> CancelExecutionAsync(
        Guid executionId,
        CancellationToken cancellationToken = default);
}
