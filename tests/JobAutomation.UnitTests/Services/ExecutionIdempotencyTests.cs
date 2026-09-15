using JobAutomation.Application.Interfaces;
using JobAutomation.Domain.Entities;
using JobAutomation.Infrastructure.Services;
using JobAutomation.Domain.Enums;
using JobAutomation.UnitTests.Infrastructure;

namespace JobAutomation.UnitTests.Services;

public class ExecutionIdempotencyTests
{
    [Fact]
    public async Task QueueManualExecution_SameKey_ReturnsSameExecution()
    {
        var (service, db, connection, jobId, _) = await CreateServiceAsync();
        await using (connection)
        await using (db)
        {
            var key = Guid.NewGuid().ToString();

            var first = await service.QueueManualExecutionAsync(jobId, key);
            var second = await service.QueueManualExecutionAsync(jobId, key);

            Assert.Equal(first.Id, second.Id);
            Assert.True(first.IsNewlyCreated);
            Assert.False(second.IsNewlyCreated);
            Assert.Equal(1, db.Executions.Count(e => e.JobId == jobId));
        }
    }

    [Fact]
    public async Task QueueManualExecution_DifferentKeys_CreateTwoExecutions()
    {
        var (service, db, connection, jobId, _) = await CreateServiceAsync();
        await using (connection)
        await using (db)
        {
            var first = await service.QueueManualExecutionAsync(jobId, Guid.NewGuid().ToString());
            var second = await service.QueueManualExecutionAsync(jobId, Guid.NewGuid().ToString());

            Assert.NotEqual(first.Id, second.Id);
            Assert.Equal(2, db.Executions.Count(e => e.JobId == jobId));
        }
    }

    [Fact]
    public async Task QueueManualExecution_ConcurrentSameKey_OneRecordBothResolve()
    {
        var (service, db, connection, jobId, _) = await CreateServiceAsync();
        await using (connection)
        await using (db)
        {
            var key = Guid.NewGuid().ToString();

            var task1 = service.QueueManualExecutionAsync(jobId, key);
            var task2 = service.QueueManualExecutionAsync(jobId, key);
            await Task.WhenAll(task1, task2);

            Assert.Equal(task1.Result.Id, task2.Result.Id);
            Assert.Equal(1, db.Executions.Count(e => e.JobId == jobId));
        }
    }

    [Fact]
    public async Task QueueManualExecution_CrossUserSameKey_DifferentExecutions()
    {
        var (db, connection) = await SqliteTestDb.CreateAsync();
        await using (connection)
        await using (db)
        {
            var key = Guid.NewGuid().ToString();
            var userA = Guid.NewGuid();
            var userB = Guid.NewGuid();
            var jobA = await SeedJobAsync(db, userA);
            var jobB = await SeedJobAsync(db, userB);

            var serviceA = CreateServiceForUser(db, userA);
            var serviceB = CreateServiceForUser(db, userB);

            var execA = await serviceA.QueueManualExecutionAsync(jobA, key);
            var execB = await serviceB.QueueManualExecutionAsync(jobB, key);

            Assert.NotEqual(execA.Id, execB.Id);
        }
    }

    private static async Task<(ExecutionService Service, ApplicationDbContext Db, Microsoft.Data.Sqlite.SqliteConnection Connection, Guid JobId, Guid UserId)>
        CreateServiceAsync()
    {
        var (db, connection) = await SqliteTestDb.CreateAsync();
        var userId = Guid.NewGuid();
        var jobId = await SeedJobAsync(db, userId);
        var service = CreateServiceForUser(db, userId);
        return (service, db, connection, jobId, userId);
    }

    private static ExecutionService CreateServiceForUser(ApplicationDbContext db, Guid userId) =>
        ExecutionServiceTestFactory.Create(
            db,
            new StubCurrentUserService(userId),
            new StubHttpJobExecutor(true));

    private static async Task<Guid> SeedJobAsync(ApplicationDbContext db, Guid userId)
    {
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
            Name = "Test Job",
            HttpMethod = "GET",
            TargetUrl = "https://example.com",
            Status = JobStatus.Active,
            TimeoutSeconds = 30,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        await SqliteTestDb.SaveAndClearAsync(db);
        return jobId;
    }

    private sealed class StubHttpJobExecutor : IHttpJobExecutor
    {
        public StubHttpJobExecutor(bool _) { }

        public Task<HttpJobExecutionResult> ExecuteAsync(Job job, CancellationToken cancellationToken = default) =>
            Task.FromResult(new HttpJobExecutionResult { Succeeded = true, HttpStatusCode = 200 });
    }

}
