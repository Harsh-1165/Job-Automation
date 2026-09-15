using JobAutomation.Application;
using JobAutomation.Application.DTOs.Executions;
using JobAutomation.Application.Interfaces;
using JobAutomation.Application.Mapping;
using JobAutomation.Application.Validation;
using JobAutomation.Domain.Entities;
using JobAutomation.Domain.Enums;
using JobAutomation.Domain.Exceptions;
using JobAutomation.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace JobAutomation.Infrastructure.Services;

public class ExecutionService : IExecutionService
{
    private const int MaxPageSize = 100;

    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IHttpJobExecutor _httpJobExecutor;
    private readonly IOutboxWriter _outboxWriter;
    private readonly IExecutionConcurrencyService _concurrency;
    private readonly IRetryPolicy _retryPolicy;
    private readonly ISystemClock _clock;
    private readonly ExecutionLeaseOptions _leaseOptions;
    private readonly ILogger<ExecutionService> _logger;

    public ExecutionService(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUser,
        IHttpJobExecutor httpJobExecutor,
        IOutboxWriter outboxWriter,
        IExecutionConcurrencyService concurrency,
        IRetryPolicy retryPolicy,
        ISystemClock clock,
        IOptions<ExecutionLeaseOptions> leaseOptions,
        ILogger<ExecutionService> logger)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _httpJobExecutor = httpJobExecutor;
        _outboxWriter = outboxWriter;
        _concurrency = concurrency;
        _retryPolicy = retryPolicy;
        _clock = clock;
        _leaseOptions = leaseOptions.Value;
        _logger = logger;
    }

    public async Task<RunJobResponse> QueueManualExecutionAsync(
        Guid jobId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var normalizedKey = IdempotencyKeyValidator.ValidateAndNormalize(idempotencyKey);
        var userId = _currentUser.GetRequiredUserId();

        var job = await _dbContext.Jobs
            .FirstOrDefaultAsync(j => j.Id == jobId && j.UserId == userId, cancellationToken);

        if (job is null)
        {
            throw new NotFoundException("Job not found.");
        }

        ValidateJobRunnable(job);

        var existing = await _dbContext.Executions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                e => e.JobId == jobId && e.IdempotencyKey == normalizedKey,
                cancellationToken);

        if (existing is not null)
        {
            _logger.LogInformation(
                "IdempotencyKeyReused JobId={JobId} UserId={UserId}",
                jobId,
                userId);

            return ExecutionMapper.ToRunResponse(existing, isNewlyCreated: false);
        }

        var now = _clock.UtcNow;
        var execution = new Execution
        {
            Id = Guid.NewGuid(),
            JobId = job.Id,
            Status = ExecutionStatus.Queued,
            TriggerType = TriggerType.Manual,
            IdempotencyKey = normalizedKey,
            AttemptNumber = 1,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _dbContext.Executions.Add(execution);
        _outboxWriter.AddExecutionEnqueue(execution.Id, now);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            var raced = await _dbContext.Executions
                .AsNoTracking()
                .FirstAsync(
                    e => e.JobId == jobId && e.IdempotencyKey == normalizedKey,
                    cancellationToken);

            _logger.LogInformation(
                "DuplicateExecutionRequest JobId={JobId} UserId={UserId}",
                jobId,
                userId);

            return ExecutionMapper.ToRunResponse(raced, isNewlyCreated: false);
        }

        _logger.LogInformation(
            "ExecutionQueued ExecutionId={ExecutionId} JobId={JobId} UserId={UserId} OutboxCreated=true",
            execution.Id,
            job.Id,
            userId);

        return ExecutionMapper.ToRunResponse(execution, isNewlyCreated: true);
    }

    public async Task<ScheduledExecutionResult> CreateScheduledExecutionAsync(
        Guid jobId,
        DateTime occurrenceUtc,
        CancellationToken cancellationToken = default)
    {
        var normalizedOccurrence = ScheduledOccurrenceKeys.TruncateToMinute(occurrenceUtc);
        var idempotencyKey = ScheduledOccurrenceKeys.Build(jobId, normalizedOccurrence);

        var job = await _dbContext.Jobs
            .AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);

        if (job is null)
        {
            _logger.LogWarning("Scheduled execution skipped; job {JobId} not found", jobId);
            return new ScheduledExecutionResult
            {
                ExecutionId = Guid.Empty,
                WasCreated = false
            };
        }

        if (job.Status != JobStatus.Active)
        {
            _logger.LogInformation(
                "ScheduledExecutionSkipped JobId={JobId} Status={Status} OccurrenceKey={OccurrenceKey}",
                jobId,
                job.Status,
                idempotencyKey);

            return new ScheduledExecutionResult
            {
                ExecutionId = Guid.Empty,
                WasCreated = false
            };
        }

        var existing = await _dbContext.Executions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                e => e.JobId == jobId && e.IdempotencyKey == idempotencyKey,
                cancellationToken);

        if (existing is not null)
        {
            _logger.LogInformation(
                "DuplicateScheduledOccurrenceIgnored JobId={JobId} ExecutionId={ExecutionId} OccurrenceKey={OccurrenceKey}",
                jobId,
                existing.Id,
                idempotencyKey);

            return new ScheduledExecutionResult
            {
                ExecutionId = existing.Id,
                WasCreated = false
            };
        }

        var now = _clock.UtcNow;
        var execution = new Execution
        {
            Id = Guid.NewGuid(),
            JobId = jobId,
            Status = ExecutionStatus.Queued,
            TriggerType = TriggerType.Scheduled,
            IdempotencyKey = idempotencyKey,
            AttemptNumber = 1,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _dbContext.Executions.Add(execution);
        _outboxWriter.AddExecutionEnqueue(execution.Id, now);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            var raced = await _dbContext.Executions
                .AsNoTracking()
                .FirstAsync(
                    e => e.JobId == jobId && e.IdempotencyKey == idempotencyKey,
                    cancellationToken);

            _logger.LogInformation(
                "DuplicateScheduledOccurrenceIgnored JobId={JobId} ExecutionId={ExecutionId} OccurrenceKey={OccurrenceKey}",
                jobId,
                raced.Id,
                idempotencyKey);

            return new ScheduledExecutionResult
            {
                ExecutionId = raced.Id,
                WasCreated = false
            };
        }

        _logger.LogInformation(
            "ScheduledExecutionCreated ExecutionId={ExecutionId} JobId={JobId} OccurrenceKey={OccurrenceKey} OutboxCreated=true",
            execution.Id,
            jobId,
            idempotencyKey);

        return new ScheduledExecutionResult
        {
            ExecutionId = execution.Id,
            WasCreated = true
        };
    }

    public async Task<RunJobResponse> RetryExecutionAsync(
        Guid executionId,
        string idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var normalizedKey = IdempotencyKeyValidator.ValidateAndNormalize(idempotencyKey);
        var userId = _currentUser.GetRequiredUserId();

        var execution = await _dbContext.Executions
            .AsNoTracking()
            .Include(e => e.Job)
            .FirstOrDefaultAsync(
                e => e.Id == executionId && e.Job.UserId == userId,
                cancellationToken);

        if (execution is null)
        {
            throw new NotFoundException("Execution not found.");
        }

        if (execution.ManualRetryIdempotencyKey == normalizedKey)
        {
            _logger.LogInformation(
                "ManualRetryIdempotencyKeyReused ExecutionId={ExecutionId}",
                executionId);

            return ExecutionMapper.ToRunResponse(execution, isNewlyCreated: false);
        }

        if (execution.Status != ExecutionStatus.Failed)
        {
            throw new ConflictException("Only failed executions can be manually retried.");
        }

        var now = _clock.UtcNow;
        var retried = await _concurrency.TryManualRetryAsync(executionId, normalizedKey, cancellationToken);

        if (!retried)
        {
            var current = await _dbContext.Executions
                .AsNoTracking()
                .FirstAsync(e => e.Id == executionId, cancellationToken);

            if (current.ManualRetryIdempotencyKey == normalizedKey)
            {
                return ExecutionMapper.ToRunResponse(current, isNewlyCreated: false);
            }

            throw new ConflictException("Execution cannot be retried in its current state.");
        }

        _outboxWriter.AddExecutionEnqueue(executionId, now);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var updated = await _dbContext.Executions
            .AsNoTracking()
            .FirstAsync(e => e.Id == executionId, cancellationToken);

        _logger.LogInformation(
            "ManualRetryRequested ExecutionId={ExecutionId} JobId={JobId} UserId={UserId}",
            executionId,
            execution.JobId,
            userId);

        return ExecutionMapper.ToRunResponse(updated, isNewlyCreated: true);
    }

    public async Task<ExecutionResponse> CancelExecutionAsync(
        Guid executionId,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetRequiredUserId();

        var execution = await _dbContext.Executions
            .AsNoTracking()
            .Include(e => e.Job)
            .FirstOrDefaultAsync(
                e => e.Id == executionId && e.Job.UserId == userId,
                cancellationToken);

        if (execution is null)
        {
            throw new NotFoundException("Execution not found.");
        }

        if (execution.Status == ExecutionStatus.Cancelled)
        {
            return ExecutionMapper.ToDetail(execution);
        }

        var cancelled = execution.Status switch
        {
            ExecutionStatus.Queued => await _concurrency.TryCancelQueuedAsync(executionId, cancellationToken),
            ExecutionStatus.Retrying => await _concurrency.TryCancelRetryingAsync(executionId, cancellationToken),
            _ => false
        };

        if (!cancelled)
        {
            throw new ConflictException(
                $"Execution in status {execution.Status} cannot be cancelled. " +
                "Cancellation is available for Queued and Retrying executions only.");
        }

        await AddLogAsync(
            executionId,
            "INFO",
            "Execution cancelled by user.",
            cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "ExecutionCancelled ExecutionId={ExecutionId} JobId={JobId} UserId={UserId}",
            executionId,
            execution.JobId,
            userId);

        var updated = await _dbContext.Executions
            .AsNoTracking()
            .Include(e => e.Job)
            .Include(e => e.Logs)
            .FirstAsync(e => e.Id == executionId, cancellationToken);

        return ExecutionMapper.ToDetail(updated);
    }

    public async Task ProcessExecutionAsync(
        Guid executionId,
        CancellationToken cancellationToken = default)
    {
        var preCheck = await _dbContext.Executions
            .AsNoTracking()
            .Select(e => new { e.Id, e.Status })
            .FirstOrDefaultAsync(e => e.Id == executionId, cancellationToken);

        if (preCheck is null)
        {
            _logger.LogWarning("Execution {ExecutionId} not found for processing", executionId);
            return;
        }

        if (preCheck.Status == ExecutionStatus.Cancelled)
        {
            _logger.LogInformation(
                "ExecutionSkipped ExecutionId={ExecutionId} Reason=Cancelled",
                executionId);
            return;
        }

        _logger.LogInformation("ExecutionClaimAttempted ExecutionId={ExecutionId}", executionId);

        var claim = await _concurrency.TryClaimAsync(executionId, cancellationToken);

        if (claim.Outcome == ExecutionClaimOutcome.NotFound)
        {
            _logger.LogWarning("Execution {ExecutionId} not found for processing", executionId);
            return;
        }

        if (claim.Outcome != ExecutionClaimOutcome.Claimed || claim.LeaseId is null)
        {
            return;
        }

        var leaseId = claim.LeaseId.Value;

        var execution = await _dbContext.Executions
            .AsNoTracking()
            .Include(e => e.Job)
            .FirstOrDefaultAsync(e => e.Id == executionId, cancellationToken);

        if (execution?.Job is null)
        {
            await _concurrency.TryCompleteFailedAsync(
                executionId,
                leaseId,
                null,
                null,
                "Associated job not found.",
                cancellationToken);
            return;
        }

        var job = execution.Job;

        if (job.Status != JobStatus.Active)
        {
            await _concurrency.TryCompleteFailedAsync(
                executionId,
                leaseId,
                null,
                null,
                "Job is no longer active.",
                cancellationToken);
            return;
        }

        await AddLogAsync(
            executionId,
            "INFO",
            $"Attempt {execution.AttemptNumber} started.",
            cancellationToken);
        await AddLogAsync(executionId, "INFO", "HTTP request sent.", cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        using var heartbeatCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var heartbeatTask = RunHeartbeatLoopAsync(executionId, leaseId, heartbeatCts.Token);

        HttpJobExecutionResult result;

        try
        {
            result = await _httpJobExecutor.ExecuteAsync(job, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ExecutionFailed ExecutionId={ExecutionId} JobId={JobId}", executionId, job.Id);

            heartbeatCts.Cancel();
            await AwaitHeartbeatTask(heartbeatTask);

            result = new HttpJobExecutionResult
            {
                Succeeded = false,
                FailureType = ExecutionFailureType.Unknown,
                ErrorMessage = "An unexpected error occurred during execution."
            };

            await HandleFailureAsync(
                executionId,
                leaseId,
                job,
                execution.AttemptNumber,
                result,
                cancellationToken);

            return;
        }

        heartbeatCts.Cancel();
        await AwaitHeartbeatTask(heartbeatTask);

        await AddLogAsync(executionId, "INFO", "HTTP response received.", cancellationToken);

        if (result.Succeeded)
        {
            var completed = await _concurrency.TryCompleteSucceededAsync(
                executionId,
                leaseId,
                result.HttpStatusCode,
                result.ResponseBody,
                cancellationToken);

            if (completed)
            {
                await AddLogAsync(
                    executionId,
                    "INFO",
                    $"Attempt {execution.AttemptNumber} succeeded.",
                    cancellationToken);
                _logger.LogInformation(
                    "ExecutionSucceeded ExecutionId={ExecutionId} JobId={JobId} Attempt={Attempt}",
                    executionId,
                    job.Id,
                    execution.AttemptNumber);
            }
            else
            {
                _logger.LogWarning(
                    "ExecutionLeaseLost ExecutionId={ExecutionId} LeaseId={LeaseId}",
                    executionId,
                    leaseId);
                return;
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await UpdateJobLastRunAsync(job.Id, cancellationToken);
            return;
        }

        await HandleFailureAsync(
            executionId,
            leaseId,
            job,
            execution.AttemptNumber,
            result,
            cancellationToken);
    }

    public async Task PrepareRetryAsync(
        Guid executionId,
        CancellationToken cancellationToken = default)
    {
        var execution = await _dbContext.Executions
            .AsNoTracking()
            .Include(e => e.Job)
            .FirstOrDefaultAsync(e => e.Id == executionId, cancellationToken);

        if (execution?.Job is null)
        {
            _logger.LogWarning("Retry preparation skipped; execution {ExecutionId} not found", executionId);
            return;
        }

        if (execution.Status == ExecutionStatus.Cancelled)
        {
            _logger.LogInformation(
                "Retry preparation skipped; execution {ExecutionId} was cancelled",
                executionId);
            return;
        }

        if (execution.Status != ExecutionStatus.Retrying)
        {
            _logger.LogInformation(
                "Retry preparation skipped; execution {ExecutionId} is {Status}",
                executionId,
                execution.Status);
            return;
        }

        if (execution.Job.Status != JobStatus.Active)
        {
            var cancelled = await _concurrency.TryCancelRetryAsync(
                executionId,
                "Job is no longer active; retry cancelled.",
                cancellationToken);

            if (cancelled)
            {
                await AddLogAsync(
                    executionId,
                    "ERROR",
                    "Retry cancelled because the job is no longer active.",
                    cancellationToken);
                await _dbContext.SaveChangesAsync(cancellationToken);
                await UpdateJobLastRunAsync(execution.JobId, cancellationToken);

                _logger.LogWarning(
                    "ExecutionRetryCancelled ExecutionId={ExecutionId} JobId={JobId}",
                    executionId,
                    execution.JobId);
            }

            return;
        }

        var prepared = await _concurrency.TryPrepareRetryForProcessingAsync(executionId, cancellationToken);

        if (!prepared)
        {
            return;
        }

        var nextAttempt = execution.AttemptNumber + 1;
        await AddLogAsync(
            executionId,
            "INFO",
            $"ExecutionRetryStarted Attempt {nextAttempt}.",
            cancellationToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        await ProcessExecutionAsync(executionId, cancellationToken);
    }

    public async Task RecoverStaleExecutionsAsync(CancellationToken cancellationToken = default)
    {
        var staleIds = await _concurrency.FindStaleExecutionIdsAsync(
            _leaseOptions.RecoveryBatchSize,
            cancellationToken);

        foreach (var executionId in staleIds)
        {
            _logger.LogInformation(
                "ExecutionLeaseExpired ExecutionId={ExecutionId}",
                executionId);

            var recovered = await _concurrency.TryRecoverStaleAsync(executionId, cancellationToken);

            if (recovered)
            {
                _outboxWriter.AddExecutionEnqueue(executionId, _clock.UtcNow);

                try
                {
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to persist outbox for recovered execution {ExecutionId}",
                        executionId);
                }
            }
        }
    }

    public async Task<ExecutionResponse> GetByIdAsync(
        Guid executionId,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetRequiredUserId();

        var execution = await _dbContext.Executions
            .AsNoTracking()
            .Include(e => e.Job)
            .Include(e => e.Logs)
            .FirstOrDefaultAsync(
                e => e.Id == executionId && e.Job.UserId == userId,
                cancellationToken);

        if (execution is null)
        {
            throw new NotFoundException("Execution not found.");
        }

        return ExecutionMapper.ToDetail(execution);
    }

    public async Task<PagedExecutionsResponse> ListByJobIdAsync(
        Guid jobId,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetRequiredUserId();

        var jobExists = await _dbContext.Jobs
            .AsNoTracking()
            .AnyAsync(j => j.Id == jobId && j.UserId == userId, cancellationToken);

        if (!jobExists)
        {
            throw new NotFoundException("Job not found.");
        }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = _dbContext.Executions
            .AsNoTracking()
            .Where(e => e.JobId == jobId);

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(e => e.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedExecutionsResponse
        {
            Items = items.Select(ExecutionMapper.ToSummary).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };
    }

    private async Task HandleFailureAsync(
        Guid executionId,
        Guid leaseId,
        Job job,
        int currentAttemptNumber,
        HttpJobExecutionResult result,
        CancellationToken cancellationToken)
    {
        var decision = _retryPolicy.Evaluate(job, result, currentAttemptNumber);

        if (!decision.ShouldRetry)
        {
            var failed = await _concurrency.TryCompleteFailedAsync(
                executionId,
                leaseId,
                result.HttpStatusCode,
                result.ResponseBody,
                result.ErrorMessage ?? decision.Reason,
                cancellationToken);

            if (!failed)
            {
                _logger.LogWarning(
                    "ExecutionLeaseLost ExecutionId={ExecutionId} LeaseId={LeaseId}",
                    executionId,
                    leaseId);
                return;
            }

            var logMessage = decision.Reason.Contains("exhausted", StringComparison.OrdinalIgnoreCase)
                ? $"Attempt {currentAttemptNumber} failed. Retries exhausted."
                : $"Attempt {currentAttemptNumber} failed. {decision.Reason}";

            await AddLogAsync(executionId, "ERROR", logMessage, cancellationToken);

            if (decision.Reason.Contains("exhausted", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "ExecutionRetryExhausted ExecutionId={ExecutionId} JobId={JobId} Attempt={Attempt}",
                    executionId,
                    job.Id,
                    currentAttemptNumber);
            }
            else
            {
                _logger.LogWarning(
                    "ExecutionNonRetryableFailure ExecutionId={ExecutionId} JobId={JobId} Attempt={Attempt}",
                    executionId,
                    job.Id,
                    currentAttemptNumber);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await UpdateJobLastRunAsync(job.Id, cancellationToken);
            return;
        }

        var nextRetryAt = _clock.UtcNow.AddSeconds(decision.DelaySeconds);
        var now = _clock.UtcNow;

        if (_dbContext is ApplicationDbContext dbContext)
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

            var scheduled = await _concurrency.TryScheduleRetryAsync(
                executionId,
                leaseId,
                result.HttpStatusCode,
                result.ResponseBody,
                result.ErrorMessage,
                nextRetryAt,
                cancellationToken);

            if (!scheduled)
            {
                _logger.LogWarning(
                    "ExecutionLeaseLost ExecutionId={ExecutionId} LeaseId={LeaseId}",
                    executionId,
                    leaseId);
                return;
            }

            _outboxWriter.AddRetryPreparation(executionId, nextRetryAt, now);

            await AddLogAsync(
                executionId,
                "WARN",
                $"Attempt {currentAttemptNumber} failed ({result.ErrorMessage ?? decision.Reason}). Retry scheduled in {decision.DelaySeconds}s.",
                cancellationToken);

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        else
        {
            var scheduled = await _concurrency.TryScheduleRetryAsync(
                executionId,
                leaseId,
                result.HttpStatusCode,
                result.ResponseBody,
                result.ErrorMessage,
                nextRetryAt,
                cancellationToken);

            if (!scheduled)
            {
                return;
            }

            _outboxWriter.AddRetryPreparation(executionId, nextRetryAt, now);

            await AddLogAsync(
                executionId,
                "WARN",
                $"Attempt {currentAttemptNumber} failed ({result.ErrorMessage ?? decision.Reason}). Retry scheduled in {decision.DelaySeconds}s.",
                cancellationToken);

            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation(
            "ExecutionRetryScheduled ExecutionId={ExecutionId} JobId={JobId} Attempt={Attempt} DelaySeconds={DelaySeconds} OutboxCreated=true",
            executionId,
            job.Id,
            currentAttemptNumber,
            decision.DelaySeconds);
    }

    private async Task RunHeartbeatLoopAsync(
        Guid executionId,
        Guid leaseId,
        CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(_leaseOptions.HeartbeatIntervalSeconds),
                    cancellationToken);

                var ok = await _concurrency.TryHeartbeatAsync(executionId, leaseId, cancellationToken);

                if (!ok)
                {
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when execution completes.
        }
    }

    private static async Task AwaitHeartbeatTask(Task heartbeatTask)
    {
        try
        {
            await heartbeatTask;
        }
        catch (OperationCanceledException)
        {
            // Expected.
        }
    }

    private static void ValidateJobRunnable(Job job)
    {
        if (job.Status == JobStatus.Archived)
        {
            throw new ConflictException("Archived jobs cannot be executed.");
        }

        if (job.Status == JobStatus.Paused)
        {
            throw new ConflictException("Paused jobs cannot be executed. Enable the job first.");
        }

        if (job.Status != JobStatus.Active)
        {
            throw new ConflictException("Only active jobs can be executed.");
        }
    }

    private async Task UpdateJobLastRunAsync(Guid jobId, CancellationToken cancellationToken)
    {
        var completedAt = await _dbContext.Executions
            .AsNoTracking()
            .Where(e => e.JobId == jobId)
            .OrderByDescending(e => e.CompletedAtUtc)
            .Select(e => e.CompletedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (completedAt is null)
        {
            return;
        }

        var job = await _dbContext.Jobs.FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
        if (job is null)
        {
            return;
        }

        job.LastRunAtUtc = completedAt;
        job.UpdatedAtUtc = _clock.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task AddLogAsync(
        Guid executionId,
        string level,
        string message,
        CancellationToken cancellationToken)
    {
        _dbContext.ExecutionLogs.Add(new ExecutionLog
        {
            Id = Guid.NewGuid(),
            ExecutionId = executionId,
            Level = level,
            Message = message,
            CreatedAtUtc = _clock.UtcNow
        });

        await Task.CompletedTask;
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
