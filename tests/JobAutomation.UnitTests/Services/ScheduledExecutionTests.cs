using JobAutomation.Application;
using JobAutomation.Application.Interfaces;
using JobAutomation.Domain;
using JobAutomation.Domain.Entities;
using JobAutomation.Domain.Enums;
using JobAutomation.Infrastructure.Persistence;
using JobAutomation.Infrastructure.Services;
using JobAutomation.UnitTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace JobAutomation.UnitTests.Services;

public class ScheduledExecutionTests
{
    [Fact]
    public async Task CreateScheduledExecution_ActiveJob_CreatesQueuedScheduledExecution()
    {
        var (service, db, connection, jobId, _) = await CreateServiceAsync();
        await using (connection)
        await using (db)
        {
            var occurrence = new DateTime(2026, 9, 14, 10, 0, 0, DateTimeKind.Utc);
            var result = await service.CreateScheduledExecutionAsync(jobId, occurrence);

            Assert.True(result.WasCreated);
            var execution = await db.Executions.AsNoTracking().FirstAsync(e => e.Id == result.ExecutionId);
            Assert.Equal(ExecutionStatus.Queued, execution.Status);
            Assert.Equal(TriggerType.Scheduled, execution.TriggerType);
            Assert.Equal(1, execution.AttemptNumber);
        }
    }

    [Fact]
    public async Task CreateScheduledExecution_DuplicateOccurrence_CreatesOneExecution()
    {
        var (service, db, connection, jobId, _) = await CreateServiceAsync();
        await using (connection)
        await using (db)
        {
            var occurrence = new DateTime(2026, 9, 14, 10, 0, 0, DateTimeKind.Utc);

            var first = await service.CreateScheduledExecutionAsync(jobId, occurrence);
            var second = await service.CreateScheduledExecutionAsync(jobId, occurrence);

            Assert.True(first.WasCreated);
            Assert.False(second.WasCreated);
            Assert.Equal(first.ExecutionId, second.ExecutionId);
            Assert.Equal(1, db.Executions.Count(e => e.JobId == jobId));
            Assert.Equal(1, db.OutboxMessages.Count(m =>
                m.MessageType == OutboxMessageTypes.ExecutionEnqueue && m.ExecutionId == first.ExecutionId));
        }
    }

    [Fact]
    public async Task CreateScheduledExecution_PausedJob_SkipsExecution()
    {
        var (service, db, connection, jobId, _) = await CreateServiceAsync(JobStatus.Paused);
        await using (connection)
        await using (db)
        {
            var result = await service.CreateScheduledExecutionAsync(
                jobId,
                new DateTime(2026, 9, 14, 10, 0, 0, DateTimeKind.Utc));

            Assert.False(result.WasCreated);
            Assert.Equal(Guid.Empty, result.ExecutionId);
            Assert.Empty(db.OutboxMessages);
        }
    }

    [Fact]
    public async Task ScheduledExecution_RetryableFailure_UsesRetryEngine()
    {
        var executor = new AttemptTrackingExecutor(503);
        var (service, db, connection, jobId, _) = await CreateServiceAsync(executor: executor);

        await using (connection)
        await using (db)
        {
            var createResult = await service.CreateScheduledExecutionAsync(
                jobId,
                new DateTime(2026, 9, 14, 10, 0, 0, DateTimeKind.Utc));

            await service.ProcessExecutionAsync(createResult.ExecutionId);

            var execution = await db.Executions.AsNoTracking().FirstAsync(e => e.Id == createResult.ExecutionId);
            Assert.Equal(ExecutionStatus.Retrying, execution.Status);
            Assert.Equal(TriggerType.Scheduled, execution.TriggerType);
            Assert.Single(db.OutboxMessages.Where(m =>
                m.MessageType == OutboxMessageTypes.RetryPreparation && m.ExecutionId == createResult.ExecutionId));
        }
    }

    [Fact]
    public async Task CreateScheduledExecution_ConcurrentDuplicate_CreatesOneExecution()
    {
        var (service, db, connection, jobId, _) = await CreateServiceAsync();
        await using (connection)
        await using (db)
        {
            var occurrence = new DateTime(2026, 9, 14, 10, 0, 0, DateTimeKind.Utc);

            var task1 = service.CreateScheduledExecutionAsync(jobId, occurrence);
            var task2 = service.CreateScheduledExecutionAsync(jobId, occurrence);
            await Task.WhenAll(task1, task2);

            Assert.Equal(task1.Result.ExecutionId, task2.Result.ExecutionId);
            Assert.Equal(1, db.Executions.Count(e => e.JobId == jobId));
        }
    }

    private static async Task<(ExecutionService Service, ApplicationDbContext Db, Microsoft.Data.Sqlite.SqliteConnection Connection, Guid JobId, StubCurrentUserService User)>
        CreateServiceAsync(
            JobStatus status = JobStatus.Active,
            IHttpJobExecutor? executor = null)
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
            Name = "Scheduled Job",
            HttpMethod = "GET",
            TargetUrl = "https://example.com",
            Status = status,
            CronExpression = "0 9 * * *",
            MaxRetries = 2,
            TimeoutSeconds = 30,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        await SqliteTestDb.SaveAndClearAsync(db);

        var user = new StubCurrentUserService(userId);
        var service = ExecutionServiceTestFactory.Create(
            db,
            user,
            executor ?? new StubHttpJobExecutor());

        return (service, db, connection, jobId, user);
    }

    private sealed class StubHttpJobExecutor : IHttpJobExecutor
    {
        public Task<HttpJobExecutionResult> ExecuteAsync(Job job, CancellationToken cancellationToken = default) =>
            Task.FromResult(new HttpJobExecutionResult { Succeeded = true, HttpStatusCode = 200 });
    }

    private sealed class AttemptTrackingExecutor : IHttpJobExecutor
    {
        private readonly int _statusCode;

        public AttemptTrackingExecutor(int statusCode) => _statusCode = statusCode;

        public Task<HttpJobExecutionResult> ExecuteAsync(Job job, CancellationToken cancellationToken = default) =>
            Task.FromResult(new HttpJobExecutionResult
            {
                Succeeded = false,
                HttpStatusCode = _statusCode,
                FailureType = Application.ExecutionFailureType.HttpResponse,
                ErrorMessage = $"External service returned HTTP {_statusCode}."
            });
    }

}
