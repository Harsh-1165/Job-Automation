using System.Net;
using System.Net.Http.Json;
using JobAutomation.Application.DTOs.Executions;
using JobAutomation.Domain;
using JobAutomation.Domain.Enums;
using JobAutomation.Infrastructure.Persistence;
using JobAutomation.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JobAutomation.IntegrationTests;

[Collection("IntegrationTests")]
public class Phase7ReliabilityIntegrationTests
{
    private readonly ApiWebApplicationFactory _factory;

    public Phase7ReliabilityIntegrationTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RetryExecution_FailedExecution_CreatesOutboxAndDispatches()
    {
        var (client, _, email) = await TestAuthHelper.RegisterUserAsync(_factory, "retry-user");
        var userId = await GetUserIdByEmailAsync(email);
        var jobId = await SeedFailedExecutionAsync(userId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var execution = await db.Executions.FirstAsync(e => e.JobId == jobId);

        var key = Guid.NewGuid();
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/executions/{execution.Id}/retry");
        request.Headers.Add("Idempotency-Key", key.ToString());
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var outbox = await db.OutboxMessages.FirstAsync(m => m.ExecutionId == execution.Id);
        Assert.Equal(OutboxMessageTypes.ExecutionEnqueue, outbox.MessageType);

        await OutboxTestHelper.DispatchAllAsync(_factory);
        Assert.Contains(execution.Id, _factory.Enqueuer.EnqueuedExecutionIds);
    }

    [Fact]
    public async Task RetryExecution_SameIdempotencyKey_ReturnsExisting()
    {
        var (client, _, email) = await TestAuthHelper.RegisterUserAsync(_factory, "retry-idem");
        var userId = await GetUserIdByEmailAsync(email);
        var jobId = await SeedFailedExecutionAsync(userId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var execution = await db.Executions.FirstAsync(e => e.JobId == jobId);
        var key = Guid.NewGuid();

        var first = await client.SendAsync(CreateRetryRequest(execution.Id, key));
        var second = await client.SendAsync(CreateRetryRequest(execution.Id, key));

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
    }

    [Fact]
    public async Task CancelExecution_Queued_MarksCancelled()
    {
        var (client, _, _) = await TestAuthHelper.RegisterUserAsync(_factory, "cancel-queued");
        var job = await CreateActiveJobAsync(client);
        var runResponse = await client.SendAsync(TestRunJobHelper.CreateRunRequest(job.Id));
        var execution = await runResponse.Content.ReadFromJsonAsync<RunJobResponse>(IntegrationTestJson.Options);
        Assert.NotNull(execution);

        var response = await client.PostAsync($"/api/executions/{execution.Id}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var detail = await response.Content.ReadFromJsonAsync<ExecutionResponse>(IntegrationTestJson.Options);
        Assert.NotNull(detail);
        Assert.Equal(ExecutionStatus.Cancelled, detail.Status);
    }

    [Fact]
    public async Task CancelExecution_OtherUsersExecution_Returns404()
    {
        var (_, _, ownerEmail) = await TestAuthHelper.RegisterUserAsync(_factory, "cancel-owner");
        var (otherClient, _, _) = await TestAuthHelper.RegisterUserAsync(_factory, "cancel-other");
        var ownerId = await GetUserIdByEmailAsync(ownerEmail);
        var jobId = await SeedQueuedExecutionAsync(ownerId);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var executionId = await db.Executions.Where(e => e.JobId == jobId).Select(e => e.Id).FirstAsync();

        var response = await otherClient.PostAsync($"/api/executions/{executionId}/cancel", null);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static HttpRequestMessage CreateRetryRequest(Guid executionId, Guid idempotencyKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/executions/{executionId}/retry");
        request.Headers.Add("Idempotency-Key", idempotencyKey.ToString());
        return request;
    }

    private async Task<Guid> SeedFailedExecutionAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = DateTime.UtcNow;
        var jobId = Guid.NewGuid();
        var executionId = Guid.NewGuid();

        db.Jobs.Add(new Domain.Entities.Job
        {
            Id = jobId,
            UserId = userId,
            Name = "Failed Job",
            HttpMethod = "GET",
            TargetUrl = "https://example.com",
            Status = JobStatus.Active,
            TimeoutSeconds = 30,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        db.Executions.Add(new Domain.Entities.Execution
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

        await db.SaveChangesAsync();
        return jobId;
    }

    private async Task<Guid> SeedQueuedExecutionAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = DateTime.UtcNow;
        var jobId = Guid.NewGuid();

        db.Jobs.Add(new Domain.Entities.Job
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

        db.Executions.Add(new Domain.Entities.Execution
        {
            Id = Guid.NewGuid(),
            JobId = jobId,
            Status = ExecutionStatus.Queued,
            TriggerType = TriggerType.Manual,
            IdempotencyKey = Guid.NewGuid().ToString(),
            AttemptNumber = 1,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        });

        await db.SaveChangesAsync();
        return jobId;
    }

    private async Task<Guid> GetUserIdByEmailAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Users
            .Where(u => u.Email == email)
            .Select(u => u.Id)
            .FirstAsync();
    }

    private static async Task<JobAutomation.Application.DTOs.Jobs.JobResponse> CreateActiveJobAsync(HttpClient client)
    {
        var createResponse = await client.PostAsJsonAsync("/api/jobs", new
        {
            name = "Active Job",
            url = "https://example.com",
            httpMethod = "GET",
            timeoutSeconds = 30
        });

        var job = await createResponse.Content.ReadFromJsonAsync<JobAutomation.Application.DTOs.Jobs.JobResponse>(IntegrationTestJson.Options);
        return job!;
    }
}
