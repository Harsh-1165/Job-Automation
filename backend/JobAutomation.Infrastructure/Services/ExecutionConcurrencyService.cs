using JobAutomation.Application;
using JobAutomation.Application.Interfaces;
using JobAutomation.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobAutomation.Infrastructure.Services;

public class ExecutionConcurrencyService : IExecutionConcurrencyService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ExecutionLeaseOptions _options;
    private readonly ILogger<ExecutionConcurrencyService> _logger;

    public ExecutionConcurrencyService(
        IApplicationDbContext dbContext,
        IOptions<ExecutionLeaseOptions> options,
        ILogger<ExecutionConcurrencyService> logger)
    {
        _dbContext = dbContext;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ExecutionClaimResult> TryClaimAsync(
        Guid executionId,
        CancellationToken cancellationToken = default)
    {
        var exists = await _dbContext.Executions
            .AsNoTracking()
            .AnyAsync(e => e.Id == executionId, cancellationToken);

        if (!exists)
        {
            return new ExecutionClaimResult { Outcome = ExecutionClaimOutcome.NotFound };
        }

        var leaseId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var leaseExpires = now.AddSeconds(_options.LeaseDurationSeconds);

        var rows = await _dbContext.Executions
            .Where(e => e.Id == executionId && e.Status == ExecutionStatus.Queued)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(e => e.Status, ExecutionStatus.Running)
                .SetProperty(e => e.StartedAtUtc, e => e.StartedAtUtc ?? now)
                .SetProperty(e => e.LeaseId, leaseId)
                .SetProperty(e => e.LeaseExpiresAtUtc, leaseExpires)
                .SetProperty(e => e.LastHeartbeatAtUtc, now)
                .SetProperty(e => e.UpdatedAtUtc, now),
                cancellationToken);

        if (rows == 1)
        {
            _logger.LogInformation(
                "ExecutionClaimed ExecutionId={ExecutionId} LeaseId={LeaseId}",
                executionId,
                leaseId);

            return new ExecutionClaimResult
            {
                Outcome = ExecutionClaimOutcome.Claimed,
                LeaseId = leaseId
            };
        }

        _logger.LogInformation(
            "ExecutionAlreadyClaimed ExecutionId={ExecutionId}",
            executionId);

        return new ExecutionClaimResult { Outcome = ExecutionClaimOutcome.AlreadyClaimed };
    }

    public async Task<bool> TryHeartbeatAsync(
        Guid executionId,
        Guid leaseId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var leaseExpires = now.AddSeconds(_options.LeaseDurationSeconds);

        var rows = await _dbContext.Executions
            .Where(e => e.Id == executionId
                        && e.Status == ExecutionStatus.Running
                        && e.LeaseId == leaseId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(e => e.LastHeartbeatAtUtc, now)
                .SetProperty(e => e.LeaseExpiresAtUtc, leaseExpires)
                .SetProperty(e => e.UpdatedAtUtc, now),
                cancellationToken);

        if (rows == 0)
        {
            _logger.LogWarning(
                "ExecutionHeartbeatFailed ExecutionId={ExecutionId} LeaseId={LeaseId}",
                executionId,
                leaseId);
        }

        return rows == 1;
    }

    public async Task<bool> TryCompleteSucceededAsync(
        Guid executionId,
        Guid leaseId,
        int? httpStatusCode,
        string? responseBody,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var rows = await _dbContext.Executions
            .Where(e => e.Id == executionId
                        && e.Status == ExecutionStatus.Running
                        && e.LeaseId == leaseId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(e => e.Status, ExecutionStatus.Succeeded)
                .SetProperty(e => e.CompletedAtUtc, now)
                .SetProperty(e => e.HttpStatusCode, httpStatusCode)
                .SetProperty(e => e.ResponseBody, responseBody)
                .SetProperty(e => e.LeaseId, (Guid?)null)
                .SetProperty(e => e.LeaseExpiresAtUtc, (DateTime?)null)
                .SetProperty(e => e.LastHeartbeatAtUtc, (DateTime?)null)
                .SetProperty(e => e.UpdatedAtUtc, now),
                cancellationToken);

        return rows == 1;
    }

    public async Task<bool> TryCompleteFailedAsync(
        Guid executionId,
        Guid leaseId,
        int? httpStatusCode,
        string? responseBody,
        string? errorMessage,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var rows = await _dbContext.Executions
            .Where(e => e.Id == executionId
                        && e.Status == ExecutionStatus.Running
                        && e.LeaseId == leaseId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(e => e.Status, ExecutionStatus.Failed)
                .SetProperty(e => e.CompletedAtUtc, now)
                .SetProperty(e => e.HttpStatusCode, httpStatusCode)
                .SetProperty(e => e.ResponseBody, responseBody)
                .SetProperty(e => e.ErrorMessage, errorMessage)
                .SetProperty(e => e.LeaseId, (Guid?)null)
                .SetProperty(e => e.LeaseExpiresAtUtc, (DateTime?)null)
                .SetProperty(e => e.LastHeartbeatAtUtc, (DateTime?)null)
                .SetProperty(e => e.UpdatedAtUtc, now),
                cancellationToken);

        return rows == 1;
    }

    public async Task<bool> TryScheduleRetryAsync(
        Guid executionId,
        Guid leaseId,
        int? httpStatusCode,
        string? responseBody,
        string? errorMessage,
        DateTime nextRetryAtUtc,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var rows = await _dbContext.Executions
            .Where(e => e.Id == executionId
                        && e.Status == ExecutionStatus.Running
                        && e.LeaseId == leaseId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(e => e.Status, ExecutionStatus.Retrying)
                .SetProperty(e => e.HttpStatusCode, httpStatusCode)
                .SetProperty(e => e.ResponseBody, responseBody)
                .SetProperty(e => e.ErrorMessage, errorMessage)
                .SetProperty(e => e.NextRetryAtUtc, nextRetryAtUtc)
                .SetProperty(e => e.LeaseId, (Guid?)null)
                .SetProperty(e => e.LeaseExpiresAtUtc, (DateTime?)null)
                .SetProperty(e => e.LastHeartbeatAtUtc, (DateTime?)null)
                .SetProperty(e => e.UpdatedAtUtc, now),
                cancellationToken);

        return rows == 1;
    }

    public async Task<bool> TryPrepareRetryForProcessingAsync(
        Guid executionId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var rows = await _dbContext.Executions
            .Where(e => e.Id == executionId && e.Status == ExecutionStatus.Retrying)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(e => e.Status, ExecutionStatus.Queued)
                .SetProperty(e => e.AttemptNumber, e => e.AttemptNumber + 1)
                .SetProperty(e => e.NextRetryAtUtc, (DateTime?)null)
                .SetProperty(e => e.UpdatedAtUtc, now),
                cancellationToken);

        if (rows == 1)
        {
            _logger.LogInformation(
                "ExecutionRetryStarted ExecutionId={ExecutionId}",
                executionId);
        }

        return rows == 1;
    }

    public async Task<bool> TryCancelRetryAsync(
        Guid executionId,
        string errorMessage,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var rows = await _dbContext.Executions
            .Where(e => e.Id == executionId && e.Status == ExecutionStatus.Retrying)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(e => e.Status, ExecutionStatus.Failed)
                .SetProperty(e => e.CompletedAtUtc, now)
                .SetProperty(e => e.ErrorMessage, errorMessage)
                .SetProperty(e => e.NextRetryAtUtc, (DateTime?)null)
                .SetProperty(e => e.UpdatedAtUtc, now),
                cancellationToken);

        return rows == 1;
    }

    public async Task<bool> TryCancelQueuedAsync(
        Guid executionId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var rows = await _dbContext.Executions
            .Where(e => e.Id == executionId && e.Status == ExecutionStatus.Queued)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(e => e.Status, ExecutionStatus.Cancelled)
                .SetProperty(e => e.CompletedAtUtc, now)
                .SetProperty(e => e.LeaseId, (Guid?)null)
                .SetProperty(e => e.LeaseExpiresAtUtc, (DateTime?)null)
                .SetProperty(e => e.LastHeartbeatAtUtc, (DateTime?)null)
                .SetProperty(e => e.NextRetryAtUtc, (DateTime?)null)
                .SetProperty(e => e.UpdatedAtUtc, now),
                cancellationToken);

        if (rows == 1)
        {
            _logger.LogInformation("ExecutionCancelled ExecutionId={ExecutionId} FromStatus=Queued", executionId);
        }

        return rows == 1;
    }

    public async Task<bool> TryCancelRetryingAsync(
        Guid executionId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var rows = await _dbContext.Executions
            .Where(e => e.Id == executionId && e.Status == ExecutionStatus.Retrying)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(e => e.Status, ExecutionStatus.Cancelled)
                .SetProperty(e => e.CompletedAtUtc, now)
                .SetProperty(e => e.NextRetryAtUtc, (DateTime?)null)
                .SetProperty(e => e.LeaseId, (Guid?)null)
                .SetProperty(e => e.LeaseExpiresAtUtc, (DateTime?)null)
                .SetProperty(e => e.LastHeartbeatAtUtc, (DateTime?)null)
                .SetProperty(e => e.UpdatedAtUtc, now),
                cancellationToken);

        if (rows == 1)
        {
            _logger.LogInformation("ExecutionCancelled ExecutionId={ExecutionId} FromStatus=Retrying", executionId);
        }

        return rows == 1;
    }

    public async Task<bool> TryManualRetryAsync(
        Guid executionId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var rows = await _dbContext.Executions
            .Where(e => e.Id == executionId && e.Status == ExecutionStatus.Failed)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(e => e.Status, ExecutionStatus.Queued)
                .SetProperty(e => e.TriggerType, TriggerType.ManualRetry)
                .SetProperty(e => e.AttemptNumber, 1)
                .SetProperty(e => e.ManualRetryIdempotencyKey, idempotencyKey)
                .SetProperty(e => e.StartedAtUtc, (DateTime?)null)
                .SetProperty(e => e.CompletedAtUtc, (DateTime?)null)
                .SetProperty(e => e.HttpStatusCode, (int?)null)
                .SetProperty(e => e.ResponseBody, (string?)null)
                .SetProperty(e => e.ErrorMessage, (string?)null)
                .SetProperty(e => e.NextRetryAtUtc, (DateTime?)null)
                .SetProperty(e => e.LeaseId, (Guid?)null)
                .SetProperty(e => e.LeaseExpiresAtUtc, (DateTime?)null)
                .SetProperty(e => e.LastHeartbeatAtUtc, (DateTime?)null)
                .SetProperty(e => e.UpdatedAtUtc, now),
                cancellationToken);

        if (rows == 1)
        {
            _logger.LogInformation(
                "ManualRetryRequested ExecutionId={ExecutionId}",
                executionId);
        }

        return rows == 1;
    }

    public async Task<bool> TryRecoverStaleAsync(
        Guid executionId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var rows = await _dbContext.Executions
            .Where(e => e.Id == executionId
                        && e.Status == ExecutionStatus.Running
                        && e.LeaseExpiresAtUtc != null
                        && e.LeaseExpiresAtUtc < now)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(e => e.Status, ExecutionStatus.Queued)
                .SetProperty(e => e.LeaseId, (Guid?)null)
                .SetProperty(e => e.LeaseExpiresAtUtc, (DateTime?)null)
                .SetProperty(e => e.LastHeartbeatAtUtc, (DateTime?)null)
                .SetProperty(e => e.UpdatedAtUtc, now),
                cancellationToken);

        if (rows == 1)
        {
            _logger.LogInformation(
                "ExecutionRecovered ExecutionId={ExecutionId}",
                executionId);
        }

        return rows == 1;
    }

    public async Task<IReadOnlyList<Guid>> FindStaleExecutionIdsAsync(
        int batchSize,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        return await _dbContext.Executions
            .AsNoTracking()
            .Where(e => e.Status == ExecutionStatus.Running
                        && e.LeaseExpiresAtUtc != null
                        && e.LeaseExpiresAtUtc < now)
            .OrderBy(e => e.LeaseExpiresAtUtc)
            .Take(batchSize)
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);
    }
}
