using JobAutomation.Application.DTOs;
using JobAutomation.Application.DTOs.Health;
using JobAutomation.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JobAutomation.Infrastructure.Services;

public class HealthCheckService : IHealthCheckService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly HangfireHealthChecker _hangfireHealthChecker;
    private readonly ILogger<HealthCheckService> _logger;

    public HealthCheckService(
        IApplicationDbContext dbContext,
        HangfireHealthChecker hangfireHealthChecker,
        ILogger<HealthCheckService> logger)
    {
        _dbContext = dbContext;
        _hangfireHealthChecker = hangfireHealthChecker;
        _logger = logger;
    }

    public async Task<HealthResponse> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        var databaseHealthy = await IsDatabaseHealthyAsync(cancellationToken);
        var hangfireHealthy = await IsHangfireHealthyAsync(cancellationToken);

        var status = databaseHealthy && hangfireHealthy ? "Healthy" : "Degraded";

        return new HealthResponse
        {
            Status = status,
            Database = databaseHealthy ? "Healthy" : "Unhealthy",
            Hangfire = hangfireHealthy ? "Healthy" : "Unhealthy"
        };
    }

    public HealthLiveResponse GetLiveness() =>
        new() { Status = "Healthy" };

    public async Task<HealthReadyResponse> GetReadinessAsync(CancellationToken cancellationToken = default)
    {
        var databaseHealthy = await IsDatabaseHealthyAsync(cancellationToken);
        var hangfireHealthy = await IsHangfireHealthyAsync(cancellationToken);
        var ready = databaseHealthy && hangfireHealthy;

        return new HealthReadyResponse
        {
            Status = ready ? "Ready" : "NotReady",
            Database = databaseHealthy,
            Hangfire = hangfireHealthy
        };
    }

    public async Task<bool> IsDatabaseHealthyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (_dbContext is DbContext dbContext)
            {
                return await dbContext.Database.CanConnectAsync(cancellationToken);
            }

            return await _dbContext.Users.AsNoTracking().AnyAsync(cancellationToken)
                   || await CanConnectViaQueryAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database health check failed");
            return false;
        }
    }

    public Task<bool> IsHangfireHealthyAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_hangfireHealthChecker.IsHealthy());

    private async Task<bool> CanConnectViaQueryAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _dbContext.Users.AsNoTracking().Take(1).CountAsync(cancellationToken);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
