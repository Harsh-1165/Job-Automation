using JobAutomation.Application;
using JobAutomation.Application.Interfaces;
using JobAutomation.Domain;
using JobAutomation.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobAutomation.Infrastructure.Services;

public class RetryRecoveryService : IRetryRecoveryService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IExecutionConcurrencyService _concurrency;
    private readonly IOutboxWriter _outboxWriter;
    private readonly OutboxOptions _options;
    private readonly ISystemClock _clock;
    private readonly ILogger<RetryRecoveryService> _logger;

    public RetryRecoveryService(
        IApplicationDbContext dbContext,
        IExecutionConcurrencyService concurrency,
        IOutboxWriter outboxWriter,
        IOptions<OutboxOptions> options,
        ISystemClock clock,
        ILogger<RetryRecoveryService> logger)
    {
        _dbContext = dbContext;
        _concurrency = concurrency;
        _outboxWriter = outboxWriter;
        _options = options.Value;
        _clock = clock;
        _logger = logger;
    }

    public async Task<int> RecoverOrphanedRetriesAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;

        var candidateIds = await _dbContext.Executions
            .AsNoTracking()
            .Where(e => e.Status == ExecutionStatus.Retrying
                        && e.NextRetryAtUtc != null
                        && e.NextRetryAtUtc <= now)
            .OrderBy(e => e.NextRetryAtUtc)
            .Take(_options.RetryRecoveryBatchSize)
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);

        var recovered = 0;

        foreach (var executionId in candidateIds)
        {
            var hasPendingRetryOutbox = await _dbContext.OutboxMessages
                .AsNoTracking()
                .AnyAsync(
                    m => m.ExecutionId == executionId
                         && m.MessageType == OutboxMessageTypes.RetryPreparation
                         && (m.Status == OutboxMessageStatus.Pending
                             || m.Status == OutboxMessageStatus.Processing),
                    cancellationToken);

            if (hasPendingRetryOutbox)
            {
                continue;
            }

            var prepared = await _concurrency.TryPrepareRetryForProcessingAsync(executionId, cancellationToken);

            if (!prepared)
            {
                continue;
            }

            _outboxWriter.AddExecutionEnqueue(executionId, now);
            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "RetryRecovery ExecutionId={ExecutionId}",
                executionId);

            recovered++;
        }

        return recovered;
    }
}
