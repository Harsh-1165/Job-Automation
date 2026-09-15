using JobAutomation.Application.Interfaces;
using JobAutomation.Domain;
using JobAutomation.Domain.Entities;
using JobAutomation.Domain.Enums;
using JobAutomation.Infrastructure.Persistence;
using JobAutomation.Infrastructure.Services;
using JobAutomation.UnitTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace JobAutomation.UnitTests.Services;

public class ExecutionServiceProcessTests
{
    [Fact]
    public async Task ProcessExecution_SuccessfulHttp_UpdatesExecutionAndJob()
    {
        var (service, db, connection, jobId, executionId, _) = await CreateServiceWithQueuedExecution();
        await using (connection)
        await using (db)
        {
            await service.ProcessExecutionAsync(executionId);

            var execution = await db.Executions.AsNoTracking().Include(e => e.Logs).FirstAsync(e => e.Id == executionId);
            var job = await db.Jobs.AsNoTracking().FirstAsync(j => j.Id == jobId);

            Assert.Equal(ExecutionStatus.Succeeded, execution.Status);
            Assert.NotNull(execution.StartedAtUtc);
            Assert.NotNull(execution.CompletedAtUtc);
            Assert.Equal(200, execution.HttpStatusCode);
            Assert.Null(execution.LeaseId);
            Assert.NotNull(job.LastRunAtUtc);
            Assert.NotEmpty(execution.Logs);
        }
    }

    [Fact]
    public async Task ProcessExecution_NonRetryableFailure_MarksFailed()
    {
        var (service, db, connection, _, executionId, _) =
            await CreateServiceWithQueuedExecution(httpStatusCode: 400, succeed: false);

        await using (connection)
        await using (db)
        {
            await service.ProcessExecutionAsync(executionId);

            var execution = await db.Executions.AsNoTracking().FirstAsync(e => e.Id == executionId);
            Assert.Equal(ExecutionStatus.Failed, execution.Status);
            Assert.Equal(400, execution.HttpStatusCode);
        }
    }

    [Fact]
    public async Task ProcessExecution_RetryableFailure_SchedulesRetry()
    {
        var (service, db, connection, _, executionId, _) =
            await CreateServiceWithQueuedExecution(httpStatusCode: 503, succeed: false);

        await using (connection)
        await using (db)
        {
            await service.ProcessExecutionAsync(executionId);

            var execution = await db.Executions.AsNoTracking().FirstAsync(e => e.Id == executionId);
            Assert.Equal(ExecutionStatus.Retrying, execution.Status);
            Assert.NotNull(execution.NextRetryAtUtc);
            Assert.Single(db.OutboxMessages.Where(m =>
                m.MessageType == OutboxMessageTypes.RetryPreparation && m.ExecutionId == executionId));
        }
    }

    [Fact]
    public async Task ProcessExecution_AlreadyCompleted_Skips()
    {
        var (service, db, connection, _, executionId, executor) = await CreateServiceWithQueuedExecution();
        await using (connection)
        await using (db)
        {
            await service.ProcessExecutionAsync(executionId);
            await service.ProcessExecutionAsync(executionId);

            Assert.Equal(1, executor.InvocationCount);
        }
    }

    private static async Task<(ExecutionService Service, ApplicationDbContext Db, Microsoft.Data.Sqlite.SqliteConnection Connection, Guid JobId, Guid ExecutionId, StubHttpJobExecutor Executor)>
        CreateServiceWithQueuedExecution(
            bool succeed = true,
            int httpStatusCode = 500)
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

        var executor = new StubHttpJobExecutor(succeed, httpStatusCode);
        var service = ExecutionServiceTestFactory.Create(
            db,
            new StubCurrentUserService(userId),
            executor);

        return (service, db, connection, jobId, executionId, executor);
    }

    private sealed class StubHttpJobExecutor : IHttpJobExecutor
    {
        private readonly bool _succeed;
        private readonly int _httpStatusCode;

        public StubHttpJobExecutor(bool succeed, int httpStatusCode = 500)
        {
            _succeed = succeed;
            _httpStatusCode = httpStatusCode;
        }

        public int InvocationCount { get; private set; }

        public Task<HttpJobExecutionResult> ExecuteAsync(Job job, CancellationToken cancellationToken = default)
        {
            InvocationCount++;
            return Task.FromResult(new HttpJobExecutionResult
            {
                Succeeded = _succeed,
                HttpStatusCode = _succeed ? 200 : _httpStatusCode,
                FailureType = _succeed
                    ? Application.ExecutionFailureType.None
                    : Application.ExecutionFailureType.HttpResponse,
                ResponseBody = _succeed ? "ok" : null,
                ErrorMessage = _succeed ? null : $"External service returned HTTP {_httpStatusCode}."
            });
        }
    }

}
