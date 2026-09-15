using JobAutomation.Application;
using JobAutomation.Application.DTOs.Dashboard;
using JobAutomation.Application.Interfaces;
using JobAutomation.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace JobAutomation.Infrastructure.Services;

public class DashboardService : IDashboardService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly ISystemClock _clock;

    public DashboardService(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUser,
        ISystemClock clock)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _clock = clock;
    }

    public async Task<DashboardSummaryResponse> GetSummaryAsync(
        DashboardTimeRange range,
        CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetRequiredUserId();
        var rangeStart = DashboardTimeRangeParser.GetStartUtc(range, _clock.UtcNow);
        var rangeLabel = FormatRangeLabel(range);

        var jobMetrics = await GetJobMetricsAsync(userId, cancellationToken);
        var executionMetrics = await GetExecutionMetricsAsync(userId, cancellationToken);
        var rates = await GetRateMetricsAsync(userId, rangeStart, cancellationToken);
        var duration = await GetDurationMetricsAsync(userId, rangeStart, cancellationToken);
        var recentExecutions = await GetRecentExecutionsAsync(userId, cancellationToken);
        var recentFailures = await GetRecentFailuresAsync(userId, cancellationToken);
        var retries = await GetRetryMetricsAsync(userId, rangeStart, cancellationToken);

        return new DashboardSummaryResponse
        {
            Range = rangeLabel,
            Jobs = jobMetrics,
            Executions = executionMetrics,
            Rates = rates,
            Duration = duration,
            RecentExecutions = recentExecutions,
            RecentFailures = recentFailures,
            Retries = retries
        };
    }

    private async Task<JobMetricsResponse> GetJobMetricsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var counts = await _dbContext.Jobs
            .AsNoTracking()
            .Where(j => j.UserId == userId)
            .GroupBy(j => j.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return new JobMetricsResponse
        {
            Total = counts.Sum(c => c.Count),
            Active = counts.FirstOrDefault(c => c.Status == JobStatus.Active)?.Count ?? 0,
            Paused = counts.FirstOrDefault(c => c.Status == JobStatus.Paused)?.Count ?? 0,
            Archived = counts.FirstOrDefault(c => c.Status == JobStatus.Archived)?.Count ?? 0
        };
    }

    private async Task<ExecutionMetricsResponse> GetExecutionMetricsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var counts = await _dbContext.Executions
            .AsNoTracking()
            .Where(e => e.Job.UserId == userId)
            .GroupBy(e => e.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return new ExecutionMetricsResponse
        {
            Total = counts.Sum(c => c.Count),
            Queued = counts.FirstOrDefault(c => c.Status == ExecutionStatus.Queued)?.Count ?? 0,
            Running = counts.FirstOrDefault(c => c.Status == ExecutionStatus.Running)?.Count ?? 0,
            Retrying = counts.FirstOrDefault(c => c.Status == ExecutionStatus.Retrying)?.Count ?? 0,
            Succeeded = counts.FirstOrDefault(c => c.Status == ExecutionStatus.Succeeded)?.Count ?? 0,
            Failed = counts.FirstOrDefault(c => c.Status == ExecutionStatus.Failed)?.Count ?? 0,
            Cancelled = counts.FirstOrDefault(c => c.Status == ExecutionStatus.Cancelled)?.Count ?? 0
        };
    }

    private async Task<RateMetricsResponse> GetRateMetricsAsync(
        Guid userId,
        DateTime rangeStart,
        CancellationToken cancellationToken)
    {
        var counts = await _dbContext.Executions
            .AsNoTracking()
            .Where(e => e.Job.UserId == userId
                        && e.CompletedAtUtc != null
                        && e.CompletedAtUtc >= rangeStart
                        && (e.Status == ExecutionStatus.Succeeded || e.Status == ExecutionStatus.Failed))
            .GroupBy(e => e.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var succeeded = counts.FirstOrDefault(c => c.Status == ExecutionStatus.Succeeded)?.Count ?? 0;
        var failed = counts.FirstOrDefault(c => c.Status == ExecutionStatus.Failed)?.Count ?? 0;
        var completed = succeeded + failed;

        return new RateMetricsResponse
        {
            CompletedCount = completed,
            SuccessRate = completed == 0 ? null : Math.Round((double)succeeded / completed, 4),
            FailureRate = completed == 0 ? null : Math.Round((double)failed / completed, 4)
        };
    }

    private async Task<DurationMetricsResponse> GetDurationMetricsAsync(
        Guid userId,
        DateTime rangeStart,
        CancellationToken cancellationToken)
    {
        var completed = await _dbContext.Executions
            .AsNoTracking()
            .Where(e => e.Job.UserId == userId
                        && e.StartedAtUtc != null
                        && e.CompletedAtUtc != null
                        && e.CompletedAtUtc >= rangeStart)
            .OrderByDescending(e => e.CompletedAtUtc)
            .Take(5000)
            .Select(e => new { e.StartedAtUtc, e.CompletedAtUtc })
            .ToListAsync(cancellationToken);

        if (completed.Count == 0)
        {
            return new DurationMetricsResponse();
        }

        var durations = completed
            .Select(e => (e.CompletedAtUtc!.Value - e.StartedAtUtc!.Value).TotalMilliseconds)
            .ToList();

        return new DurationMetricsResponse
        {
            AverageMs = Math.Round(durations.Average(), 2),
            MinMs = durations.Min(),
            MaxMs = durations.Max()
        };
    }

    private async Task<IReadOnlyList<RecentExecutionResponse>> GetRecentExecutionsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Executions
            .AsNoTracking()
            .Where(e => e.Job.UserId == userId)
            .OrderByDescending(e => e.CreatedAtUtc)
            .Take(20)
            .Select(e => new RecentExecutionResponse
            {
                Id = e.Id,
                JobId = e.JobId,
                JobName = e.Job.Name,
                Status = e.Status,
                TriggerType = e.TriggerType,
                Attempt = e.AttemptNumber,
                CreatedAtUtc = e.CreatedAtUtc,
                CompletedAtUtc = e.CompletedAtUtc,
                DurationMs = e.StartedAtUtc != null && e.CompletedAtUtc != null
                    ? (long?)(e.CompletedAtUtc.Value - e.StartedAtUtc.Value).TotalMilliseconds
                    : null
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<RecentFailureResponse>> GetRecentFailuresAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Executions
            .AsNoTracking()
            .Where(e => e.Job.UserId == userId && e.Status == ExecutionStatus.Failed)
            .OrderByDescending(e => e.CompletedAtUtc)
            .Take(10)
            .Select(e => new RecentFailureResponse
            {
                Id = e.Id,
                JobId = e.JobId,
                JobName = e.Job.Name,
                HttpStatusCode = e.HttpStatusCode,
                ErrorMessage = e.ErrorMessage,
                Attempt = e.AttemptNumber,
                CompletedAtUtc = e.CompletedAtUtc ?? e.UpdatedAtUtc
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<RetryMetricsResponse> GetRetryMetricsAsync(
        Guid userId,
        DateTime rangeStart,
        CancellationToken cancellationToken)
    {
        var currentlyRetrying = await _dbContext.Executions
            .AsNoTracking()
            .CountAsync(
                e => e.Job.UserId == userId && e.Status == ExecutionStatus.Retrying,
                cancellationToken);

        var retriesInRange = await _dbContext.Executions
            .AsNoTracking()
            .CountAsync(
                e => e.Job.UserId == userId
                     && e.AttemptNumber > 1
                     && e.CreatedAtUtc >= rangeStart,
                cancellationToken);

        var succeededAfterRetry = await _dbContext.Executions
            .AsNoTracking()
            .CountAsync(
                e => e.Job.UserId == userId
                     && e.Status == ExecutionStatus.Succeeded
                     && e.AttemptNumber > 1
                     && e.CompletedAtUtc != null
                     && e.CompletedAtUtc >= rangeStart,
                cancellationToken);

        return new RetryMetricsResponse
        {
            CurrentlyRetrying = currentlyRetrying,
            RetriesInRange = retriesInRange,
            SucceededAfterRetryInRange = succeededAfterRetry
        };
    }

    private static string FormatRangeLabel(DashboardTimeRange range) =>
        range switch
        {
            DashboardTimeRange.Hours24 => "24h",
            DashboardTimeRange.Days7 => "7d",
            DashboardTimeRange.Days30 => "30d",
            _ => "24h"
        };
}
