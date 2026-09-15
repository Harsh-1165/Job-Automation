using JobAutomation.Application;
using JobAutomation.Application.Interfaces;
using JobAutomation.Domain;
using JobAutomation.Domain.Entities;
using JobAutomation.Domain.Enums;
using JobAutomation.Infrastructure.Persistence;
using JobAutomation.Infrastructure.Services;
using JobAutomation.UnitTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace JobAutomation.UnitTests.Services;

public class ExecutionProcessingConcurrencyTests
{
    [Fact]
    public async Task ProcessExecution_DuplicateHangfireDelivery_HttpCalledOnce()
    {
        var (service, db, connection, _, executionId, executor) = await CreateServiceWithQueuedExecutionAsync();
        await using (connection)
        await using (db)
        {
            var task1 = service.ProcessExecutionAsync(executionId);
            var task2 = service.ProcessExecutionAsync(executionId);
            await Task.WhenAll(task1, task2);

            Assert.Equal(1, executor.InvocationCount);
        }
    }

    [Fact]
    public async Task ProcessExecution_StaleRecovery_RequeuesExecution()
    {
        var (service, db, connection, _, executionId, executor) =
            await CreateServiceWithQueuedExecutionAsync();

        await using (connection)
        await using (db)
        {
            var leaseOptions = Options.Create(new ExecutionLeaseOptions
            {
                LeaseDurationSeconds = 1,
                HeartbeatIntervalSeconds = 1,
                RecoveryBatchSize = 100
            });

            var concurrency = new ExecutionConcurrencyService(
                db,
                leaseOptions,
                NullLogger<ExecutionConcurrencyService>.Instance);

            service = ExecutionServiceTestFactory.Create(
                db,
                new StubCurrentUserService(Guid.NewGuid()),
                executor,
                concurrency,
                leaseOptions: leaseOptions.Value);

            var claim = await concurrency.TryClaimAsync(executionId);
            Assert.Equal(ExecutionClaimOutcome.Claimed, claim.Outcome);

            var execution = await db.Executions.FirstAsync(e => e.Id == executionId);
            execution.LeaseExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
            await SqliteTestDb.SaveAndClearAsync(db);

            await service.RecoverStaleExecutionsAsync();

            execution = await db.Executions.AsNoTracking().FirstAsync(e => e.Id == executionId);
            Assert.Equal(ExecutionStatus.Queued, execution.Status);
            Assert.Contains(executionId, db.OutboxMessages
                .Where(m => m.MessageType == OutboxMessageTypes.ExecutionEnqueue)
                .Select(m => m.ExecutionId));
        }
    }

    [Fact]
    public async Task ProcessExecution_OldWorkerCannotOverwriteAfterRecovery()
    {
        var (service, db, connection, _, executionId, executor) = await CreateServiceWithQueuedExecutionAsync();
        await using (connection)
        await using (db)
        {
            var leaseOptions = Options.Create(new ExecutionLeaseOptions());
            var concurrency = new ExecutionConcurrencyService(
                db,
                leaseOptions,
                NullLogger<ExecutionConcurrencyService>.Instance);

            var claimA = await concurrency.TryClaimAsync(executionId);
            var leaseA = claimA.LeaseId!.Value;

            var execution = await db.Executions.FirstAsync(e => e.Id == executionId);
            execution.LeaseExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
            await SqliteTestDb.SaveAndClearAsync(db);

            await concurrency.TryRecoverStaleAsync(executionId);

            var claimB = await concurrency.TryClaimAsync(executionId);
            var leaseB = claimB.LeaseId!.Value;

            var oldWorkerComplete = await concurrency.TryCompleteSucceededAsync(
                executionId,
                leaseA,
                200,
                "old");

            Assert.False(oldWorkerComplete);

            var newWorkerComplete = await concurrency.TryCompleteSucceededAsync(
                executionId,
                leaseB,
                200,
                "new");

            Assert.True(newWorkerComplete);

            execution = await db.Executions.AsNoTracking().FirstAsync(e => e.Id == executionId);
            Assert.Equal(ExecutionStatus.Succeeded, execution.Status);
            Assert.Equal("new", execution.ResponseBody);
            Assert.Equal(0, executor.InvocationCount);
        }
    }

    [Fact]
    public async Task StaleRecovery_DoesNotTouchRetryingExecutions()
    {
        var (db, connection) = await SqliteTestDb.CreateAsync();
        await using (connection)
        await using (db)
        {
            var userId = Guid.NewGuid();
            var jobId = Guid.NewGuid();
            var executionId = Guid.NewGuid();
            var now = DateTime.UtcNow;

            db.Users.Add(new User
            {
                Id = userId,
                Email = "retrying@example.com",
                NormalizedEmail = "RETRYING@EXAMPLE.COM",
                PasswordHash = "hash",
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });

            db.Jobs.Add(new Job
            {
                Id = jobId,
                UserId = userId,
                Name = "Retrying Job",
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
                NextRetryAtUtc = now.AddMinutes(5),
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
            await SqliteTestDb.SaveAndClearAsync(db);

            var concurrency = new ExecutionConcurrencyService(
                db,
                Options.Create(new ExecutionLeaseOptions()),
                NullLogger<ExecutionConcurrencyService>.Instance);

            var staleIds = await concurrency.FindStaleExecutionIdsAsync(100);
            Assert.Empty(staleIds);
        }
    }

    private static async Task<(ExecutionService Service, ApplicationDbContext Db, Microsoft.Data.Sqlite.SqliteConnection Connection, Guid JobId, Guid ExecutionId, CountingHttpJobExecutor Executor)>
        CreateServiceWithQueuedExecutionAsync()
    {
        var (db, connection) = await SqliteTestDb.CreateAsync();
        var userId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var executionId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        db.Users.Add(new User
        {
            Id = userId,
            Email = "test@example.com",
            NormalizedEmail = "test@example.com",
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
            MaxRetries = 3,
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

        var executor = new CountingHttpJobExecutor();
        var service = ExecutionServiceTestFactory.Create(
            db,
            new StubCurrentUserService(userId),
            executor);

        return (service, db, connection, jobId, executionId, executor);
    }

    private sealed class CountingHttpJobExecutor : IHttpJobExecutor
    {
        public int InvocationCount { get; private set; }

        public Task<HttpJobExecutionResult> ExecuteAsync(Job job, CancellationToken cancellationToken = default)
        {
            InvocationCount++;
            return Task.FromResult(new HttpJobExecutionResult
            {
                Succeeded = true,
                HttpStatusCode = 200,
                ResponseBody = "ok"
            });
        }
    }

}
