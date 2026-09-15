using JobAutomation.Application.Interfaces;
using JobAutomation.Domain;
using JobAutomation.Domain.Entities;
using JobAutomation.Domain.Enums;
using JobAutomation.Domain.Exceptions;
using JobAutomation.Infrastructure.Persistence;
using JobAutomation.Infrastructure.Services;
using JobAutomation.UnitTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace JobAutomation.UnitTests.Services;

public class ManualRetryAndCancellationTests
{
    [Fact]
    public async Task ManualRetry_FailedExecution_QueuesWithOutbox()
    {
        var (service, db, connection, executionId, userId) = await CreateFailedExecutionAsync();
        await using (connection)
        await using (db)
        {
            var key = Guid.NewGuid().ToString();
            var response = await service.RetryExecutionAsync(executionId, key);

            Assert.True(response.IsNewlyCreated);
            Assert.Equal(ExecutionStatus.Queued, response.Status);
            Assert.Equal(TriggerType.ManualRetry, response.TriggerType);

            var execution = await db.Executions.AsNoTracking().FirstAsync(e => e.Id == executionId);
            Assert.Equal(1, execution.AttemptNumber);
            Assert.Equal(key.ToLowerInvariant(), execution.ManualRetryIdempotencyKey);
            Assert.Single(db.OutboxMessages.Where(m =>
                m.MessageType == OutboxMessageTypes.ExecutionEnqueue && m.ExecutionId == executionId));
        }
    }

    [Fact]
    public async Task ManualRetry_SameIdempotencyKey_IsIdempotent()
    {
        var (service, db, connection, executionId, _) = await CreateFailedExecutionAsync();
        await using (connection)
        await using (db)
        {
            var key = Guid.NewGuid().ToString();
            var first = await service.RetryExecutionAsync(executionId, key);
            var second = await service.RetryExecutionAsync(executionId, key);

            Assert.Equal(first.Id, second.Id);
            Assert.True(first.IsNewlyCreated);
            Assert.False(second.IsNewlyCreated);
            Assert.Equal(1, db.OutboxMessages.Count(m =>
                m.MessageType == OutboxMessageTypes.ExecutionEnqueue && m.ExecutionId == executionId));
        }
    }

    [Fact]
    public async Task ManualRetry_NonFailed_ThrowsConflict()
    {
        var (service, db, connection, executionId, _) = await CreateFailedExecutionAsync();
        await using (connection)
        await using (db)
        {
            var execution = await db.Executions.FirstAsync(e => e.Id == executionId);
            execution.Status = ExecutionStatus.Queued;
            await SqliteTestDb.SaveAndClearAsync(db);

            await Assert.ThrowsAsync<ConflictException>(() =>
                service.RetryExecutionAsync(executionId, Guid.NewGuid().ToString()));
        }
    }

    [Fact]
    public async Task Cancel_QueuedExecution_MarksCancelled()
    {
        var (service, db, connection, executionId, _) = await CreateQueuedExecutionAsync();
        await using (connection)
        await using (db)
        {
            var response = await service.CancelExecutionAsync(executionId);
            Assert.Equal(ExecutionStatus.Cancelled, response.Status);

            await service.ProcessExecutionAsync(executionId);
            var execution = await db.Executions.AsNoTracking().FirstAsync(e => e.Id == executionId);
            Assert.Equal(ExecutionStatus.Cancelled, execution.Status);
        }
    }

    [Fact]
    public async Task Cancel_RetryingExecution_MarksCancelled()
    {
        var (service, db, connection, executionId, _) = await CreateRetryingExecutionAsync();
        await using (connection)
        await using (db)
        {
            var response = await service.CancelExecutionAsync(executionId);
            Assert.Equal(ExecutionStatus.Cancelled, response.Status);

            await service.PrepareRetryAsync(executionId);
            var execution = await db.Executions.AsNoTracking().FirstAsync(e => e.Id == executionId);
            Assert.Equal(ExecutionStatus.Cancelled, execution.Status);
        }
    }

    [Fact]
    public async Task Cancel_Succeeded_ThrowsConflict()
    {
        var (service, db, connection, executionId, _) = await CreateQueuedExecutionAsync();
        await using (connection)
        await using (db)
        {
            var execution = await db.Executions.FirstAsync(e => e.Id == executionId);
            execution.Status = ExecutionStatus.Succeeded;
            execution.CompletedAtUtc = DateTime.UtcNow;
            await SqliteTestDb.SaveAndClearAsync(db);

            await Assert.ThrowsAsync<ConflictException>(() => service.CancelExecutionAsync(executionId));
        }
    }

    [Fact]
    public async Task ManualRetry_CrossUser_ReturnsNotFound()
    {
        var (service, db, connection, executionId, _) = await CreateFailedExecutionAsync();
        await using (connection)
        await using (db)
        {
            var otherUserService = ExecutionServiceTestFactory.Create(
                db,
                new StubCurrentUserService(Guid.NewGuid()),
                new StubHttpJobExecutor());

            await Assert.ThrowsAsync<NotFoundException>(() =>
                otherUserService.RetryExecutionAsync(executionId, Guid.NewGuid().ToString()));
        }
    }

    private static async Task<(ExecutionService Service, ApplicationDbContext Db, Microsoft.Data.Sqlite.SqliteConnection Connection, Guid ExecutionId, Guid UserId)>
        CreateFailedExecutionAsync()
    {
        var (db, connection) = await SqliteTestDb.CreateAsync();
        var userId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var executionId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        SeedUserJob(db, userId, jobId, now);
        db.Executions.Add(new Execution
        {
            Id = executionId,
            JobId = jobId,
            Status = ExecutionStatus.Failed,
            TriggerType = TriggerType.Manual,
            IdempotencyKey = Guid.NewGuid().ToString(),
            AttemptNumber = 2,
            CompletedAtUtc = now,
            ErrorMessage = "failed",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        await SqliteTestDb.SaveAndClearAsync(db);

        var service = ExecutionServiceTestFactory.Create(
            db,
            new StubCurrentUserService(userId),
            new StubHttpJobExecutor());

        return (service, db, connection, executionId, userId);
    }

    private static async Task<(ExecutionService Service, ApplicationDbContext Db, Microsoft.Data.Sqlite.SqliteConnection Connection, Guid ExecutionId, Guid UserId)>
        CreateQueuedExecutionAsync()
    {
        var (db, connection) = await SqliteTestDb.CreateAsync();
        var userId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var executionId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        SeedUserJob(db, userId, jobId, now);
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

        var service = ExecutionServiceTestFactory.Create(
            db,
            new StubCurrentUserService(userId),
            new StubHttpJobExecutor());

        return (service, db, connection, executionId, userId);
    }

    private static async Task<(ExecutionService Service, ApplicationDbContext Db, Microsoft.Data.Sqlite.SqliteConnection Connection, Guid ExecutionId, Guid UserId)>
        CreateRetryingExecutionAsync()
    {
        var (db, connection) = await SqliteTestDb.CreateAsync();
        var userId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var executionId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        SeedUserJob(db, userId, jobId, now);
        db.Executions.Add(new Execution
        {
            Id = executionId,
            JobId = jobId,
            Status = ExecutionStatus.Retrying,
            TriggerType = TriggerType.Manual,
            IdempotencyKey = Guid.NewGuid().ToString(),
            AttemptNumber = 1,
            NextRetryAtUtc = now.AddMinutes(1),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
        await SqliteTestDb.SaveAndClearAsync(db);

        var service = ExecutionServiceTestFactory.Create(
            db,
            new StubCurrentUserService(userId),
            new StubHttpJobExecutor());

        return (service, db, connection, executionId, userId);
    }

    private static void SeedUserJob(ApplicationDbContext db, Guid userId, Guid jobId, DateTime now)
    {
        db.Users.Add(new User
        {
            Id = userId,
            Email = $"user-{userId:N}@example.com",
            NormalizedEmail = $"user-{userId:N}@example.com",
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
    }

    private sealed class StubHttpJobExecutor : IHttpJobExecutor
    {
        public Task<HttpJobExecutionResult> ExecuteAsync(Job job, CancellationToken cancellationToken = default) =>
            Task.FromResult(new HttpJobExecutionResult { Succeeded = true, HttpStatusCode = 200 });
    }

}
