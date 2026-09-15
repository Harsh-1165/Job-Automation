using JobAutomation.Application.Interfaces;
using JobAutomation.Domain;
using JobAutomation.Domain.Entities;
using JobAutomation.Domain.Enums;
using JobAutomation.Infrastructure.Persistence;
using JobAutomation.Infrastructure.Services;
using JobAutomation.UnitTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace JobAutomation.UnitTests.Outbox;

public class OutboxTransactionalTests
{
    [Fact]
    public async Task QueueManualExecution_CreatesExecutionAndOutboxAtomically()
    {
        var (service, db, connection, jobId, _) = await CreateServiceAsync();
        await using (connection)
        await using (db)
        {
            var key = Guid.NewGuid().ToString();
            var response = await service.QueueManualExecutionAsync(jobId, key);

            Assert.True(response.IsNewlyCreated);

            var execution = await db.Executions.AsNoTracking().FirstAsync(e => e.Id == response.Id);
            Assert.Equal(ExecutionStatus.Queued, execution.Status);

            var outbox = await db.OutboxMessages.AsNoTracking()
                .FirstAsync(m => m.ExecutionId == response.Id);
            Assert.Equal(OutboxMessageTypes.ExecutionEnqueue, outbox.MessageType);
            Assert.Equal(OutboxMessageStatus.Pending, outbox.Status);
        }
    }

    [Fact]
    public async Task DuplicateOutboxPublish_DoesNotCauseDuplicateHttpExecution()
    {
        var executor = new CountingHttpJobExecutor();
        var (service, db, connection, _, executionId) = await CreateQueuedExecutionAsync(executor);
        await using (connection)
        await using (db)
        {
            await service.ProcessExecutionAsync(executionId);
            await service.ProcessExecutionAsync(executionId);

            Assert.Equal(1, executor.InvocationCount);
            Assert.Equal(ExecutionStatus.Succeeded,
                (await db.Executions.AsNoTracking().FirstAsync(e => e.Id == executionId)).Status);
        }
    }

    private static async Task<(ExecutionService Service, ApplicationDbContext Db, Microsoft.Data.Sqlite.SqliteConnection Connection, Guid JobId, Guid UserId)>
        CreateServiceAsync()
    {
        var (db, connection) = await SqliteTestDb.CreateAsync();
        var userId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var now = DateTime.UtcNow;

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
            Name = "Outbox Job",
            HttpMethod = "GET",
            TargetUrl = "https://example.com",
            Status = JobStatus.Active,
            TimeoutSeconds = 30,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        await SqliteTestDb.SaveAndClearAsync(db);

        var service = ExecutionServiceTestFactory.Create(
            db,
            new StubCurrentUserService(userId),
            new CountingHttpJobExecutor());

        return (service, db, connection, jobId, userId);
    }

    private static async Task<(ExecutionService Service, ApplicationDbContext Db, Microsoft.Data.Sqlite.SqliteConnection Connection, Guid JobId, Guid ExecutionId)>
        CreateQueuedExecutionAsync(IHttpJobExecutor executor)
    {
        var (db, connection) = await SqliteTestDb.CreateAsync();
        var userId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var executionId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        db.Users.Add(new User
        {
            Id = userId,
            Email = "outbox@example.com",
            NormalizedEmail = "outbox@example.com",
            PasswordHash = "hash",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        db.Jobs.Add(new Job
        {
            Id = jobId,
            UserId = userId,
            Name = "Queued Job",
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

        var service = ExecutionServiceTestFactory.Create(
            db,
            new StubCurrentUserService(userId),
            executor);

        return (service, db, connection, jobId, executionId);
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
                HttpStatusCode = 200
            });
        }
    }

}
