using JobAutomation.Application.Interfaces;
using JobAutomation.Domain.Entities;
using JobAutomation.Infrastructure.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace JobAutomation.Infrastructure.Services;

public class WorkerHeartbeatService : IWorkerHeartbeatService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly WorkerIdentity _identity;
    private readonly ISystemClock _clock;
    private readonly ILogger<WorkerHeartbeatService> _logger;

    public WorkerHeartbeatService(
        IApplicationDbContext dbContext,
        WorkerIdentity identity,
        ISystemClock clock,
        ILogger<WorkerHeartbeatService> logger)
    {
        _dbContext = dbContext;
        _identity = identity;
        _clock = clock;
        _logger = logger;
    }

    public async Task RecordHeartbeatAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var rows = await _dbContext.WorkerHeartbeats
            .Where(w => w.WorkerId == _identity.WorkerId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(w => w.LastHeartbeatAtUtc, now),
                cancellationToken);

        if (rows == 0)
        {
            _dbContext.WorkerHeartbeats.Add(new WorkerHeartbeat
            {
                Id = Guid.NewGuid(),
                WorkerId = _identity.WorkerId,
                HostName = _identity.HostName,
                StartedAtUtc = _identity.StartedAtUtc,
                LastHeartbeatAtUtc = now
            });

            await _dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "WorkerHeartbeatRegistered WorkerId={WorkerId} HostName={HostName}",
                _identity.WorkerId,
                _identity.HostName);
        }
    }

    public async Task RecordProcessingAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;

        await _dbContext.WorkerHeartbeats
            .Where(w => w.WorkerId == _identity.WorkerId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(w => w.LastProcessedAtUtc, now)
                .SetProperty(w => w.LastHeartbeatAtUtc, now),
                cancellationToken);
    }
}
