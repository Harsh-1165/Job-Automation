using JobAutomation.Application;
using JobAutomation.Domain;
using JobAutomation.Domain.Entities;
using JobAutomation.Domain.Enums;
using JobAutomation.Infrastructure.Outbox;
using JobAutomation.Infrastructure.Persistence;
using JobAutomation.Infrastructure.Services;
using JobAutomation.UnitTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace JobAutomation.UnitTests.Services;

public class RetryRecoveryServiceTests
{
    [Fact]
    public async Task RecoverOrphanedRetries_TransitionsToQueuedAndCreatesOutbox()
    {
        var (db, connection) = await SqliteTestDb.CreateAsync();
        await using (connection)
        await using (db)
        {
            var executionId = Guid.NewGuid();
            var jobId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var now = DateTime.UtcNow;

            db.Users.Add(new User
            {
                Id = userId,
                Email = "recover@example.com",
                NormalizedEmail = "recover@example.com",
                PasswordHash = "hash",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });

            db.Jobs.Add(new Job
            {
                Id = jobId,
                UserId = userId,
                Name = "Recover Job",
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
                Status = ExecutionStatus.Retrying,
                TriggerType = TriggerType.Manual,
                IdempotencyKey = Guid.NewGuid().ToString(),
                AttemptNumber = 1,
                NextRetryAtUtc = now.AddMinutes(-1),
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
            await SqliteTestDb.SaveAndClearAsync(db);

            var service = CreateRecoveryService(db);
            var recovered = await service.RecoverOrphanedRetriesAsync();

            Assert.Equal(1, recovered);
            var execution = await db.Executions.AsNoTracking().FirstAsync(e => e.Id == executionId);
            Assert.Equal(ExecutionStatus.Queued, execution.Status);
            Assert.Equal(2, execution.AttemptNumber);
            Assert.Single(db.OutboxMessages.Where(m =>
                m.MessageType == OutboxMessageTypes.ExecutionEnqueue && m.ExecutionId == executionId));
        }
    }

    [Fact]
    public async Task RecoverOrphanedRetries_SkipsWhenRetryOutboxExists()
    {
        var (db, connection) = await SqliteTestDb.CreateAsync();
        await using (connection)
        await using (db)
        {
            var executionId = Guid.NewGuid();
            var jobId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var now = DateTime.UtcNow;

            db.Users.Add(new User
            {
                Id = userId,
                Email = "retry@example.com",
                NormalizedEmail = "retry@example.com",
                PasswordHash = "hash",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });

            db.Jobs.Add(new Job
            {
                Id = jobId,
                UserId = userId,
                Name = "Retry Job",
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
                Status = ExecutionStatus.Retrying,
                TriggerType = TriggerType.Manual,
                IdempotencyKey = Guid.NewGuid().ToString(),
                AttemptNumber = 1,
                NextRetryAtUtc = now.AddMinutes(-1),
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });

            db.OutboxMessages.Add(new OutboxMessage
            {
                Id = Guid.NewGuid(),
                MessageType = OutboxMessageTypes.RetryPreparation,
                Payload = "{}",
                ExecutionId = executionId,
                Status = OutboxMessageStatus.Pending,
                CreatedAtUtc = now
            });
            await SqliteTestDb.SaveAndClearAsync(db);

            var service = CreateRecoveryService(db);
            var recovered = await service.RecoverOrphanedRetriesAsync();

            Assert.Equal(0, recovered);
        }
    }

    private static RetryRecoveryService CreateRecoveryService(ApplicationDbContext db)
    {
        var leaseOptions = Options.Create(new ExecutionLeaseOptions());
        return new RetryRecoveryService(
            db,
            new ExecutionConcurrencyService(db, leaseOptions, NullLogger<ExecutionConcurrencyService>.Instance),
            new OutboxWriter(db),
            Options.Create(new OutboxOptions { RetryRecoveryBatchSize = 50 }),
            new FixedClock(DateTime.UtcNow),
            NullLogger<RetryRecoveryService>.Instance);
    }
}
