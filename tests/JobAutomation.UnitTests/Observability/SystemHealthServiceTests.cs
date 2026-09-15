using JobAutomation.Application;
using JobAutomation.Application.Interfaces;
using JobAutomation.Domain;
using JobAutomation.Domain.Entities;
using JobAutomation.Domain.Enums;
using JobAutomation.Infrastructure.Persistence;
using JobAutomation.Infrastructure.Services;
using JobAutomation.UnitTests.Infrastructure;
using Microsoft.Extensions.Options;

namespace JobAutomation.UnitTests.Observability;

public class SystemHealthServiceTests
{
    [Fact]
    public async Task GetSystemHealth_FailedOutbox_IsUnhealthy()
    {
        var (db, connection) = await SqliteTestDb.CreateAsync();
        await using (connection)
        await using (db)
        {
            var now = DateTime.UtcNow;
            db.OutboxMessages.Add(new OutboxMessage
            {
                Id = Guid.NewGuid(),
                MessageType = OutboxMessageTypes.ExecutionEnqueue,
                Payload = "{}",
                Status = OutboxMessageStatus.Failed,
                CreatedAtUtc = now
            });
            await SqliteTestDb.SaveAndClearAsync(db);

            var service = CreateService(db, databaseHealthy: true, hangfireHealthy: true);
            var health = await service.GetSystemHealthAsync();

            Assert.Equal("Unhealthy", health.Status);
            Assert.Equal(1, health.Outbox.Failed);
        }
    }

    [Fact]
    public async Task GetSystemHealth_StaleWorker_IsDegradedOrUnhealthy()
    {
        var (db, connection) = await SqliteTestDb.CreateAsync();
        await using (connection)
        await using (db)
        {
            var now = DateTime.UtcNow;
            db.WorkerHeartbeats.Add(new WorkerHeartbeat
            {
                Id = Guid.NewGuid(),
                WorkerId = "worker-test",
                HostName = "test-host",
                StartedAtUtc = now.AddHours(-1),
                LastHeartbeatAtUtc = now.AddMinutes(-10)
            });
            await SqliteTestDb.SaveAndClearAsync(db);

            var clock = new FixedClock(now);
            var service = new SystemHealthService(
                db,
                new StubHealthCheckService(true, true),
                Options.Create(new ObservabilityOptions { WorkerStaleThresholdSeconds = 60 }),
                clock);

            var health = await service.GetSystemHealthAsync();

            Assert.True(health.Workers.Stale >= 1);
            Assert.NotEqual("Healthy", health.Workers.Status);
        }
    }

    private static SystemHealthService CreateService(
        ApplicationDbContext db,
        bool databaseHealthy,
        bool hangfireHealthy) =>
        new(
            db,
            new StubHealthCheckService(databaseHealthy, hangfireHealthy),
            Options.Create(new ObservabilityOptions()),
            new FixedClock(DateTime.UtcNow));

    private sealed class StubHealthCheckService : IHealthCheckService
    {
        private readonly bool _db;
        private readonly bool _hangfire;

        public StubHealthCheckService(bool db, bool hangfire)
        {
            _db = db;
            _hangfire = hangfire;
        }

        public Task<Application.DTOs.HealthResponse> GetHealthAsync(CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Application.DTOs.Health.HealthLiveResponse GetLiveness() =>
            new() { Status = "Healthy" };

        public Task<Application.DTOs.Health.HealthReadyResponse> GetReadinessAsync(CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<bool> IsDatabaseHealthyAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_db);

        public Task<bool> IsHangfireHealthyAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_hangfire);
    }
}
