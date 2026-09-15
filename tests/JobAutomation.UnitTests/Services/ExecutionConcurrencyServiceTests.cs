using JobAutomation.Application;
using JobAutomation.Domain.Entities;
using JobAutomation.Domain.Enums;
using JobAutomation.Infrastructure.Persistence;
using JobAutomation.Infrastructure.Services;
using JobAutomation.UnitTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace JobAutomation.UnitTests.Services;

public class ExecutionConcurrencyServiceTests
{
    private static ExecutionConcurrencyService CreateService(
        ApplicationDbContext db,
        ExecutionLeaseOptions? options = null)
    {
        return new ExecutionConcurrencyService(
            db,
            Options.Create(options ?? new ExecutionLeaseOptions
            {
                LeaseDurationSeconds = 60,
                HeartbeatIntervalSeconds = 20,
                RecoveryBatchSize = 100
            }),
            NullLogger<ExecutionConcurrencyService>.Instance);
    }

    [Fact]
    public async Task TryClaimAsync_ConcurrentClaims_ExactlyOneSucceeds()
    {
        var (db, connection) = await SqliteTestDb.CreateAsync();
        await using (connection)
        await using (db)
        {
            var executionId = await SeedQueuedExecutionAsync(db);
            var service = CreateService(db);

            var claim1 = service.TryClaimAsync(executionId);
            var claim2 = service.TryClaimAsync(executionId);
            await Task.WhenAll(claim1, claim2);

            var results = new[] { claim1.Result, claim2.Result };
            Assert.Equal(1, results.Count(r => r.Outcome == ExecutionClaimOutcome.Claimed));
            Assert.Equal(1, results.Count(r => r.Outcome == ExecutionClaimOutcome.AlreadyClaimed));

            var execution = await db.Executions.AsNoTracking().FirstAsync(e => e.Id == executionId);
            Assert.Equal(ExecutionStatus.Running, execution.Status);
            Assert.NotNull(execution.LeaseId);
        }
    }

    [Fact]
    public async Task TryRecoverStaleAsync_DoubleRecovery_OnlyOneSucceeds()
    {
        var (db, connection) = await SqliteTestDb.CreateAsync();
        await using (connection)
        await using (db)
        {
            var executionId = await SeedRunningExecutionAsync(
                db,
                leaseExpiresAtUtc: DateTime.UtcNow.AddMinutes(-1));

            var service = CreateService(db);

            var recover1 = service.TryRecoverStaleAsync(executionId);
            var recover2 = service.TryRecoverStaleAsync(executionId);
            await Task.WhenAll(recover1, recover2);

            Assert.True(recover1.Result ^ recover2.Result);

            var execution = await db.Executions.AsNoTracking().FirstAsync(e => e.Id == executionId);
            Assert.Equal(ExecutionStatus.Queued, execution.Status);
            Assert.Null(execution.LeaseId);
        }
    }

    [Fact]
    public async Task TryCompleteSucceededAsync_WrongLeaseId_DoesNotUpdate()
    {
        var (db, connection) = await SqliteTestDb.CreateAsync();
        await using (connection)
        await using (db)
        {
            var executionId = await SeedRunningExecutionAsync(db, leaseId: Guid.NewGuid());
            var service = CreateService(db);

            var completed = await service.TryCompleteSucceededAsync(
                executionId,
                Guid.NewGuid(),
                200,
                "ok");

            Assert.False(completed);

            var execution = await db.Executions.AsNoTracking().FirstAsync(e => e.Id == executionId);
            Assert.Equal(ExecutionStatus.Running, execution.Status);
        }
    }

    [Fact]
    public async Task TryHeartbeatAsync_WrongLeaseId_Fails()
    {
        var (db, connection) = await SqliteTestDb.CreateAsync();
        await using (connection)
        await using (db)
        {
            var leaseId = Guid.NewGuid();
            var executionId = await SeedRunningExecutionAsync(db, leaseId: leaseId);
            var service = CreateService(db);

            var ok = await service.TryHeartbeatAsync(executionId, Guid.NewGuid());
            Assert.False(ok);
        }
    }

    [Fact]
    public async Task TryHeartbeatAsync_ExtendsLease_ForHealthyExecution()
    {
        var (db, connection) = await SqliteTestDb.CreateAsync();
        await using (connection)
        await using (db)
        {
            var leaseId = Guid.NewGuid();
            var executionId = await SeedRunningExecutionAsync(
                db,
                leaseId: leaseId,
                leaseExpiresAtUtc: DateTime.UtcNow.AddSeconds(5));

            var service = CreateService(db);
            var before = (await db.Executions.AsNoTracking().FirstAsync(e => e.Id == executionId))!.LeaseExpiresAtUtc;

            var ok = await service.TryHeartbeatAsync(executionId, leaseId);
            Assert.True(ok);

            var after = (await db.Executions.AsNoTracking().FirstAsync(e => e.Id == executionId))!.LeaseExpiresAtUtc;
            Assert.True(after > before);
        }
    }

    [Fact]
    public async Task TryRecoverStaleAsync_HealthyLease_DoesNotRecover()
    {
        var (db, connection) = await SqliteTestDb.CreateAsync();
        await using (connection)
        await using (db)
        {
            var executionId = await SeedRunningExecutionAsync(
                db,
                leaseExpiresAtUtc: DateTime.UtcNow.AddMinutes(5));

            var service = CreateService(db);
            var recovered = await service.TryRecoverStaleAsync(executionId);

            Assert.False(recovered);

            var execution = await db.Executions.AsNoTracking().FirstAsync(e => e.Id == executionId);
            Assert.Equal(ExecutionStatus.Running, execution.Status);
        }
    }

    [Fact]
    public async Task FindStaleExecutionIdsAsync_ReturnsExpiredRunningExecutions()
    {
        var (db, connection) = await SqliteTestDb.CreateAsync();
        await using (connection)
        await using (db)
        {
            var staleId = await SeedRunningExecutionAsync(
                db,
                leaseExpiresAtUtc: DateTime.UtcNow.AddMinutes(-1));
            await SeedRunningExecutionAsync(
                db,
                leaseExpiresAtUtc: DateTime.UtcNow.AddMinutes(5));

            var service = CreateService(db);
            var staleIds = await service.FindStaleExecutionIdsAsync(100);

            Assert.Single(staleIds);
            Assert.Equal(staleId, staleIds[0]);
        }
    }

    private static async Task<Guid> SeedQueuedExecutionAsync(ApplicationDbContext db)
    {
        var userId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var executionId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        db.Users.Add(new User
        {
            Id = userId,
            Email = $"test-{executionId:N}@example.com",
            NormalizedEmail = $"test-{executionId:N}@example.com",
            PasswordHash = "hash",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        db.Jobs.Add(new Job
        {
            Id = jobId,
            UserId = userId,
            Name = "Test Job",
            HttpMethod = "GET",
            TargetUrl = "https://example.com",
            Status = JobStatus.Active,
            TimeoutSeconds = 30,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        db.Executions.Add(new Execution
        {
            Id = executionId,
            JobId = jobId,
            Status = ExecutionStatus.Queued,
            TriggerType = TriggerType.Manual,
            IdempotencyKey = Guid.NewGuid().ToString(),
            AttemptNumber = 1,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        await SqliteTestDb.SaveAndClearAsync(db);
        return executionId;
    }

    private static async Task<Guid> SeedRunningExecutionAsync(
        ApplicationDbContext db,
        Guid? leaseId = null,
        DateTime? leaseExpiresAtUtc = null)
    {
        var executionId = await SeedQueuedExecutionAsync(db);
        var execution = await db.Executions.FirstAsync(e => e.Id == executionId);
        execution.Status = ExecutionStatus.Running;
        execution.StartedAtUtc = DateTime.UtcNow;
        execution.LeaseId = leaseId ?? Guid.NewGuid();
        execution.LeaseExpiresAtUtc = leaseExpiresAtUtc ?? DateTime.UtcNow.AddMinutes(1);
        execution.LastHeartbeatAtUtc = DateTime.UtcNow;
        await SqliteTestDb.SaveAndClearAsync(db);
        return executionId;
    }
}
