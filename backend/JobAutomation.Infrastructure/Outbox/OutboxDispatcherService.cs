using JobAutomation.Application;
using JobAutomation.Application.Interfaces;
using JobAutomation.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobAutomation.Infrastructure.Outbox;

public class OutboxDispatcherService : IOutboxDispatcher
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IOutboxPublisher _publisher;
    private readonly OutboxOptions _options;
    private readonly ISystemClock _clock;
    private readonly ILogger<OutboxDispatcherService> _logger;

    public OutboxDispatcherService(
        IApplicationDbContext dbContext,
        IOutboxPublisher publisher,
        IOptions<OutboxOptions> options,
        ISystemClock clock,
        ILogger<OutboxDispatcherService> logger)
    {
        _dbContext = dbContext;
        _publisher = publisher;
        _options = options.Value;
        _clock = clock;
        _logger = logger;
    }

    public async Task<int> DispatchBatchAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        await ReclaimExpiredLocksAsync(now, cancellationToken);

        var candidateIds = await _dbContext.OutboxMessages
            .AsNoTracking()
            .Where(m => m.Status == OutboxMessageStatus.Pending
                        && (m.NextAttemptAtUtc == null || m.NextAttemptAtUtc <= now)
                        && (m.LockedUntilUtc == null || m.LockedUntilUtc <= now))
            .OrderBy(m => m.CreatedAtUtc)
            .Take(_options.BatchSize)
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);

        var processed = 0;

        foreach (var messageId in candidateIds)
        {
            if (!await TryClaimMessageAsync(messageId, now, cancellationToken))
            {
                continue;
            }

            var message = await _dbContext.OutboxMessages
                .AsNoTracking()
                .FirstAsync(m => m.Id == messageId, cancellationToken);

            try
            {
                await _publisher.PublishAsync(message, cancellationToken);
                await MarkProcessedAsync(messageId, now, cancellationToken);

                _logger.LogInformation(
                    "OutboxPublished OutboxId={OutboxId} MessageType={MessageType} ExecutionId={ExecutionId}",
                    messageId,
                    message.MessageType,
                    message.ExecutionId);

                processed++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "OutboxPublishFailed OutboxId={OutboxId} MessageType={MessageType} ExecutionId={ExecutionId} Attempt={Attempt}",
                    messageId,
                    message.MessageType,
                    message.ExecutionId,
                    message.AttemptCount + 1);

                await MarkPublishFailedAsync(messageId, ex.Message, now, cancellationToken);
            }
        }

        return processed;
    }

    public async Task<int> CleanupProcessedAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = _clock.UtcNow.AddDays(-_options.ProcessedRetentionDays);

        var staleIds = await _dbContext.OutboxMessages
            .AsNoTracking()
            .Where(m => m.Status == OutboxMessageStatus.Processed
                        && m.ProcessedAtUtc != null
                        && m.ProcessedAtUtc < cutoff)
            .OrderBy(m => m.ProcessedAtUtc)
            .Take(_options.CleanupBatchSize)
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);

        if (staleIds.Count == 0)
        {
            return 0;
        }

        var deleted = await _dbContext.OutboxMessages
            .Where(m => staleIds.Contains(m.Id))
            .ExecuteDeleteAsync(cancellationToken);

        if (deleted > 0)
        {
            _logger.LogInformation("OutboxCleanupRemoved Count={Count}", deleted);
        }

        return deleted;
    }

    private async Task ReclaimExpiredLocksAsync(DateTime now, CancellationToken cancellationToken)
    {
        var reclaimed = await _dbContext.OutboxMessages
            .Where(m => m.Status == OutboxMessageStatus.Processing
                        && m.LockedUntilUtc != null
                        && m.LockedUntilUtc <= now)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(m => m.Status, OutboxMessageStatus.Pending)
                .SetProperty(m => m.LockId, (Guid?)null)
                .SetProperty(m => m.LockedUntilUtc, (DateTime?)null),
                cancellationToken);

        if (reclaimed > 0)
        {
            _logger.LogWarning("OutboxReclaimed Count={Count}", reclaimed);
        }
    }

    private async Task<bool> TryClaimMessageAsync(
        Guid messageId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var lockId = Guid.NewGuid();
        var lockedUntil = now.AddSeconds(_options.LeaseDurationSeconds);

        var rows = await _dbContext.OutboxMessages
            .Where(m => m.Id == messageId
                        && m.Status == OutboxMessageStatus.Pending
                        && (m.NextAttemptAtUtc == null || m.NextAttemptAtUtc <= now)
                        && (m.LockedUntilUtc == null || m.LockedUntilUtc <= now))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(m => m.Status, OutboxMessageStatus.Processing)
                .SetProperty(m => m.LockId, lockId)
                .SetProperty(m => m.LockedUntilUtc, lockedUntil),
                cancellationToken);

        if (rows == 1)
        {
            _logger.LogInformation("OutboxClaimed OutboxId={OutboxId} LockId={LockId}", messageId, lockId);
        }

        return rows == 1;
    }

    private async Task MarkProcessedAsync(Guid messageId, DateTime now, CancellationToken cancellationToken)
    {
        await _dbContext.OutboxMessages
            .Where(m => m.Id == messageId && m.Status == OutboxMessageStatus.Processing)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(m => m.Status, OutboxMessageStatus.Processed)
                .SetProperty(m => m.ProcessedAtUtc, now)
                .SetProperty(m => m.LockId, (Guid?)null)
                .SetProperty(m => m.LockedUntilUtc, (DateTime?)null)
                .SetProperty(m => m.LastError, (string?)null),
                cancellationToken);
    }

    private async Task MarkPublishFailedAsync(
        Guid messageId,
        string error,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var message = await _dbContext.OutboxMessages
            .AsNoTracking()
            .FirstAsync(m => m.Id == messageId, cancellationToken);

        var nextAttempt = message.AttemptCount + 1;
        var delaySeconds = CalculateBackoffSeconds(nextAttempt);
        var nextAttemptAt = now.AddSeconds(delaySeconds);
        var failedPermanently = nextAttempt >= _options.MaxPublishAttempts;

        if (failedPermanently)
        {
            await _dbContext.OutboxMessages
                .Where(m => m.Id == messageId && m.Status == OutboxMessageStatus.Processing)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(m => m.Status, OutboxMessageStatus.Failed)
                    .SetProperty(m => m.AttemptCount, nextAttempt)
                    .SetProperty(m => m.LastError, TruncateError(error))
                    .SetProperty(m => m.LockId, (Guid?)null)
                    .SetProperty(m => m.LockedUntilUtc, (DateTime?)null),
                    cancellationToken);
        }
        else
        {
            await _dbContext.OutboxMessages
                .Where(m => m.Id == messageId && m.Status == OutboxMessageStatus.Processing)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(m => m.Status, OutboxMessageStatus.Pending)
                    .SetProperty(m => m.AttemptCount, nextAttempt)
                    .SetProperty(m => m.LastError, TruncateError(error))
                    .SetProperty(m => m.NextAttemptAtUtc, nextAttemptAt)
                    .SetProperty(m => m.LockId, (Guid?)null)
                    .SetProperty(m => m.LockedUntilUtc, (DateTime?)null),
                    cancellationToken);
        }
    }

    private int CalculateBackoffSeconds(int attemptCount)
    {
        var exponent = Math.Min(Math.Max(attemptCount - 1, 0), 10);
        var delay = (long)_options.BaseRetryDelaySeconds * (1L << exponent);
        return (int)Math.Min(delay, _options.MaxRetryDelaySeconds);
    }

    private static string TruncateError(string error) =>
        error.Length <= 4000 ? error : error[..4000];
}
