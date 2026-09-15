using JobAutomation.Infrastructure.Persistence;
using JobAutomation.Infrastructure.Services;
using JobAutomation.Infrastructure.Worker;
using JobAutomation.UnitTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace JobAutomation.UnitTests.Observability;

public class WorkerHeartbeatServiceTests
{
    [Fact]
    public async Task RecordHeartbeat_CreatesThenUpdatesSameWorker()
    {
        var (db, connection) = await SqliteTestDb.CreateAsync();
        await using (connection)
        await using (db)
        {
            var identity = new WorkerIdentity();
            var clock = new FixedClock(DateTime.UtcNow);
            var service = new WorkerHeartbeatService(db, identity, clock, NullLogger<WorkerHeartbeatService>.Instance);

            await service.RecordHeartbeatAsync();
            clock.UtcNow = clock.UtcNow.AddSeconds(30);
            await service.RecordHeartbeatAsync();

            Assert.Equal(1, await db.WorkerHeartbeats.CountAsync());
            var heartbeat = await db.WorkerHeartbeats.AsNoTracking().FirstAsync();
            Assert.Equal(identity.WorkerId, heartbeat.WorkerId);
            Assert.Equal(clock.UtcNow, heartbeat.LastHeartbeatAtUtc);
        }
    }

    [Fact]
    public async Task MultipleWorkers_CreateSeparateRecords()
    {
        var (db, connection) = await SqliteTestDb.CreateAsync();
        await using (connection)
        await using (db)
        {
            var identityA = new WorkerIdentity();
            var identityB = new WorkerIdentity();
            var clock = new FixedClock(DateTime.UtcNow);

            var serviceA = new WorkerHeartbeatService(db, identityA, clock, NullLogger<WorkerHeartbeatService>.Instance);
            var serviceB = new WorkerHeartbeatService(db, identityB, clock, NullLogger<WorkerHeartbeatService>.Instance);

            await serviceA.RecordHeartbeatAsync();
            await serviceB.RecordHeartbeatAsync();

            Assert.Equal(2, await db.WorkerHeartbeats.CountAsync());
        }
    }
}
