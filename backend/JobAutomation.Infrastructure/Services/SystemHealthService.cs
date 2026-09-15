using JobAutomation.Application;
using JobAutomation.Application.DTOs.Admin;
using JobAutomation.Application.Interfaces;
using JobAutomation.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JobAutomation.Infrastructure.Services;

public class SystemHealthService : ISystemHealthService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IHealthCheckService _healthCheckService;
    private readonly ObservabilityOptions _options;
    private readonly ISystemClock _clock;

    public SystemHealthService(
        IApplicationDbContext dbContext,
        IHealthCheckService healthCheckService,
        IOptions<ObservabilityOptions> options,
        ISystemClock clock)
    {
        _dbContext = dbContext;
        _healthCheckService = healthCheckService;
        _options = options.Value;
        _clock = clock;
    }

    public async Task<SystemHealthResponse> GetSystemHealthAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var databaseHealthy = await _healthCheckService.IsDatabaseHealthyAsync(cancellationToken);
        var hangfireHealthy = await _healthCheckService.IsHangfireHealthyAsync(cancellationToken);

        var outbox = await GetOutboxHealthAsync(now, cancellationToken);
        var workers = await GetWorkerHealthAsync(now, cancellationToken);

        var overall = ResolveOverallStatus(databaseHealthy, hangfireHealthy, outbox.Status, workers.Status);

        return new SystemHealthResponse
        {
            Status = overall,
            Database = new ComponentHealthResponse
            {
                Status = databaseHealthy ? "Healthy" : "Unhealthy"
            },
            Hangfire = new ComponentHealthResponse
            {
                Status = hangfireHealthy ? "Healthy" : "Unhealthy"
            },
            Outbox = outbox,
            Workers = workers
        };
    }

    private async Task<OutboxHealthResponse> GetOutboxHealthAsync(
        DateTime now,
        CancellationToken cancellationToken)
    {
        var counts = await _dbContext.OutboxMessages
            .AsNoTracking()
            .GroupBy(m => m.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var pending = counts.FirstOrDefault(c => c.Status == OutboxMessageStatus.Pending)?.Count ?? 0;
        var processing = counts.FirstOrDefault(c => c.Status == OutboxMessageStatus.Processing)?.Count ?? 0;
        var failed = counts.FirstOrDefault(c => c.Status == OutboxMessageStatus.Failed)?.Count ?? 0;

        var oldestPending = await _dbContext.OutboxMessages
            .AsNoTracking()
            .Where(m => m.Status == OutboxMessageStatus.Pending)
            .OrderBy(m => m.CreatedAtUtc)
            .Select(m => (DateTime?)m.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        double? oldestAgeMinutes = oldestPending is null
            ? null
            : Math.Round((now - oldestPending.Value).TotalMinutes, 2);

        var status = ResolveOutboxStatus(pending, failed, oldestAgeMinutes);

        return new OutboxHealthResponse
        {
            Status = status,
            Pending = pending,
            Processing = processing,
            Failed = failed,
            OldestPendingAgeMinutes = oldestAgeMinutes
        };
    }

    private async Task<WorkerHealthSummaryResponse> GetWorkerHealthAsync(
        DateTime now,
        CancellationToken cancellationToken)
    {
        var heartbeats = await _dbContext.WorkerHeartbeats
            .AsNoTracking()
            .OrderByDescending(w => w.LastHeartbeatAtUtc)
            .ToListAsync(cancellationToken);

        var items = heartbeats.Select(w =>
        {
            var secondsSince = (now - w.LastHeartbeatAtUtc).TotalSeconds;
            var isHealthy = secondsSince <= _options.WorkerStaleThresholdSeconds;

            return new WorkerStatusResponse
            {
                WorkerId = w.WorkerId,
                HostName = w.HostName,
                Status = isHealthy ? "Healthy" : "Stale",
                LastHeartbeatAtUtc = w.LastHeartbeatAtUtc,
                SecondsSinceHeartbeat = Math.Round(secondsSince, 1),
                LastProcessedAtUtc = w.LastProcessedAtUtc
            };
        }).ToList();

        var healthy = items.Count(i => i.Status == "Healthy");
        var stale = items.Count - healthy;

        var workerStatus = heartbeats.Count == 0
            ? "Unknown"
            : healthy > 0
                ? stale > 0 ? "Degraded" : "Healthy"
                : "Unhealthy";

        return new WorkerHealthSummaryResponse
        {
            Status = workerStatus,
            Total = items.Count,
            Healthy = healthy,
            Stale = stale,
            Items = items
        };
    }

    private string ResolveOutboxStatus(int pending, int failed, double? oldestAgeMinutes)
    {
        if (failed > 0)
        {
            return "Unhealthy";
        }

        if (pending >= _options.OutboxPendingUnhealthyThreshold
            || (oldestAgeMinutes ?? 0) >= _options.OutboxOldestPendingUnhealthyMinutes)
        {
            return "Unhealthy";
        }

        if (pending >= _options.OutboxPendingDegradedThreshold
            || (oldestAgeMinutes ?? 0) >= _options.OutboxOldestPendingDegradedMinutes)
        {
            return "Degraded";
        }

        return "Healthy";
    }

    private static string ResolveOverallStatus(
        bool databaseHealthy,
        bool hangfireHealthy,
        string outboxStatus,
        string workerStatus)
    {
        if (!databaseHealthy || outboxStatus == "Unhealthy" || workerStatus == "Unhealthy")
        {
            return "Unhealthy";
        }

        if (!hangfireHealthy || outboxStatus == "Degraded" || workerStatus == "Degraded")
        {
            return "Degraded";
        }

        return workerStatus == "Unknown" ? "Degraded" : "Healthy";
    }
}
