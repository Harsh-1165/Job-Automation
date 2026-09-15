using JobAutomation.Application;
using JobAutomation.Application.Interfaces;
using JobAutomation.Domain.Entities;
using JobAutomation.Domain.Enums;
using JobAutomation.Infrastructure.Persistence;
using JobAutomation.Infrastructure.Services;
using JobAutomation.UnitTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace JobAutomation.UnitTests.Services;

public class ExecutionRetryTests
{
    [Fact]
    public async Task PrepareRetry_InactiveJob_CancelsRetry()
    {
        var (service, db, connection, jobId, executionId) = await CreateRetryingExecutionAsync();
        await using (connection)
        await using (db)
        {
            var job = await db.Jobs.FirstAsync(j => j.Id == jobId);
            job.Status = JobStatus.Paused;
            await SqliteTestDb.SaveAndClearAsync(db);

            await service.PrepareRetryAsync(executionId);

            var execution = await db.Executions.AsNoTracking().FirstAsync(e => e.Id == executionId);
            Assert.Equal(ExecutionStatus.Failed, execution.Status);
            Assert.Contains("no longer active", execution.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task PrepareRetry_ActiveJob_ProcessesNextAttempt()
    {
        var executor = new AttemptTrackingExecutor([200]);
        var (service, db, connection, _, executionId) =
            await CreateRetryingExecutionAsync(executor: executor);

        await using (connection)
        await using (db)
        {
            await service.PrepareRetryAsync(executionId);

            var execution = await db.Executions.AsNoTracking().FirstAsync(e => e.Id == executionId);
            Assert.Equal(ExecutionStatus.Succeeded, execution.Status);
            Assert.Equal(2, execution.AttemptNumber);
            Assert.Equal(1, executor.InvocationCount);
        }
    }

    [Fact]
    public async Task MaxRetries_TwoFailuresThenFinalFailure()
    {
        var executor = new AttemptTrackingExecutor([503, 503, 503]);
        var (service, db, connection, _, executionId) =
            await CreateQueuedExecutionAsync(maxRetries: 2, executor: executor);

        await using (connection)
        await using (db)
        {
            await service.ProcessExecutionAsync(executionId);
            Assert.Equal(ExecutionStatus.Retrying, (await db.Executions.AsNoTracking().FirstAsync(e => e.Id == executionId))!.Status);

            await service.PrepareRetryAsync(executionId);
            Assert.Equal(ExecutionStatus.Retrying, (await db.Executions.AsNoTracking().FirstAsync(e => e.Id == executionId))!.Status);

            await service.PrepareRetryAsync(executionId);
            var execution = await db.Executions.AsNoTracking().FirstAsync(e => e.Id == executionId);
            Assert.Equal(ExecutionStatus.Failed, execution.Status);
            Assert.Equal(3, execution.AttemptNumber);
            Assert.Equal(3, executor.InvocationCount);
        }
    }

    [Fact]
    public async Task IdempotencyKeyReusedDuringRetry_ReturnsSameExecution()
    {
        var (service, db, connection, jobId, _) = await CreateQueuedExecutionAsync();
        await using (connection)
        await using (db)
        {
            var key = Guid.NewGuid().ToString();
            var first = await service.QueueManualExecutionAsync(jobId, key);

            var execution = await db.Executions.FirstAsync(e => e.Id == first.Id);
            execution.Status = ExecutionStatus.Retrying;
            execution.NextRetryAtUtc = DateTime.UtcNow.AddMinutes(1);
            await SqliteTestDb.SaveAndClearAsync(db);

            var second = await service.QueueManualExecutionAsync(jobId, key);
            Assert.Equal(first.Id, second.Id);
            Assert.Equal(1, db.Executions.Count(e => e.JobId == jobId && e.IdempotencyKey == key));
        }
    }

    private static async Task<(ExecutionService Service, ApplicationDbContext Db, Microsoft.Data.Sqlite.SqliteConnection Connection, Guid JobId, Guid ExecutionId)>
        CreateQueuedExecutionAsync(
            int maxRetries = 3,
            IHttpJobExecutor? executor = null)
    {
        var (db, connection) = await SqliteTestDb.CreateAsync();
        var userId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var executionId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        SeedUserJobExecution(db, userId, jobId, executionId, maxRetries, now, ExecutionStatus.Queued);
        await SqliteTestDb.SaveAndClearAsync(db);

        var service = ExecutionServiceTestFactory.Create(
            db,
            new StubCurrentUserService(userId),
            executor ?? new AttemptTrackingExecutor([503]));

        return (service, db, connection, jobId, executionId);
    }

    private static async Task<(ExecutionService Service, ApplicationDbContext Db, Microsoft.Data.Sqlite.SqliteConnection Connection, Guid JobId, Guid ExecutionId)>
        CreateRetryingExecutionAsync(
            IHttpJobExecutor? executor = null)
    {
        var (db, connection) = await SqliteTestDb.CreateAsync();
        var userId = Guid.NewGuid();
        var jobId = Guid.NewGuid();
        var executionId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        SeedUserJobExecution(db, userId, jobId, executionId, 3, now, ExecutionStatus.Retrying);
        await db.SaveChangesAsync();
        var execution = await db.Executions.FirstAsync(e => e.Id == executionId);
        execution.NextRetryAtUtc = now.AddSeconds(10);
        execution.AttemptNumber = 1;
        await SqliteTestDb.SaveAndClearAsync(db);

        var service = ExecutionServiceTestFactory.Create(
            db,
            new StubCurrentUserService(userId),
            executor ?? new AttemptTrackingExecutor([200]));

        return (service, db, connection, jobId, executionId);
    }

    private static void SeedUserJobExecution(
        ApplicationDbContext db,
        Guid userId,
        Guid jobId,
        Guid executionId,
        int maxRetries,
        DateTime now,
        ExecutionStatus status)
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
            Name = "Retry Job",
            HttpMethod = "GET",
            TargetUrl = "https://example.com",
            Status = JobStatus.Active,
            MaxRetries = maxRetries,
            TimeoutSeconds = 30,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        db.Executions.Add(new Execution
        {
            Id = executionId,
            JobId = jobId,
            Status = status,
            TriggerType = TriggerType.Manual,
            IdempotencyKey = Guid.NewGuid().ToString(),
            AttemptNumber = 1,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });
    }

    private sealed class AttemptTrackingExecutor : IHttpJobExecutor
    {
        private readonly Queue<int> _statusCodes;

        public AttemptTrackingExecutor(int[] statusCodes)
        {
            _statusCodes = new Queue<int>(statusCodes);
        }

        public int InvocationCount { get; private set; }

        public Task<HttpJobExecutionResult> ExecuteAsync(Job job, CancellationToken cancellationToken = default)
        {
            InvocationCount++;
            var statusCode = _statusCodes.Count > 0 ? _statusCodes.Dequeue() : 200;
            var succeeded = statusCode >= 200 && statusCode < 300;

            return Task.FromResult(new HttpJobExecutionResult
            {
                Succeeded = succeeded,
                HttpStatusCode = statusCode,
                FailureType = succeeded ? ExecutionFailureType.None : ExecutionFailureType.HttpResponse,
                ErrorMessage = succeeded ? null : $"External service returned HTTP {statusCode}."
            });
        }
    }

}
