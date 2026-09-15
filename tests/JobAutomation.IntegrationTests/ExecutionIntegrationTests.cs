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
public class ExecutionIntegrationTests
{
    private readonly ApiWebApplicationFactory _factory;
    private readonly FakeBackgroundJobEnqueuer _enqueuer;

    public ExecutionIntegrationTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
        _enqueuer = factory.Enqueuer;
    }

    [Fact]
    public async Task RunJob_CreatesQueuedExecution()
    {
        _enqueuer.EnqueuedExecutionIds.Clear();
        var client = _factory.CreateClient();
        var (authedClient, _, _) = await TestAuthHelper.RegisterUserAsync(_factory);

        var createResponse = await authedClient.PostAsJsonAsync("/api/jobs", new
        {
            name = "Run Test Job",
            url = "https://httpbin.org/get",
            httpMethod = "GET",
            timeoutSeconds = 30
        });

        var job = await createResponse.Content.ReadFromJsonAsync<JobAutomation.Application.DTOs.Jobs.JobResponse>(IntegrationTestJson.Options);
        Assert.NotNull(job);

        var runResponse = await authedClient.SendAsync(TestRunJobHelper.CreateRunRequest(job.Id));
        Assert.Equal(HttpStatusCode.Accepted, runResponse.StatusCode);

        var execution = await runResponse.Content.ReadFromJsonAsync<RunJobResponse>(IntegrationTestJson.Options);
        Assert.NotNull(execution);
        Assert.Equal(ExecutionStatus.Queued, execution.Status);
        Assert.Equal(TriggerType.Manual, execution.TriggerType);
        Assert.Equal(1, execution.Attempt);
        Assert.True(execution.IsNewlyCreated);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var stored = await db.Executions.FirstAsync(e => e.Id == execution.Id);
        Assert.Equal(ExecutionStatus.Queued, stored.Status);

        var outbox = await db.OutboxMessages.FirstAsync(m => m.ExecutionId == execution.Id);
        Assert.Equal(OutboxMessageTypes.ExecutionEnqueue, outbox.MessageType);
        Assert.Equal(OutboxMessageStatus.Pending, outbox.Status);

        await OutboxTestHelper.DispatchAllAsync(_factory);
        Assert.Contains(execution.Id, _enqueuer.EnqueuedExecutionIds);
    }

    [Fact]
    public async Task RunJob_SameIdempotencyKey_ReturnsExistingExecution()
    {
        _enqueuer.EnqueuedExecutionIds.Clear();
        var client = _factory.CreateClient();
        var (authedClient, _, _) = await TestAuthHelper.RegisterUserAsync(_factory);

        var createResponse = await authedClient.PostAsJsonAsync("/api/jobs", new
        {
            name = "Idempotent Job",
            url = "https://example.com",
            httpMethod = "GET",
            timeoutSeconds = 30
        });

        var job = await createResponse.Content.ReadFromJsonAsync<JobAutomation.Application.DTOs.Jobs.JobResponse>(IntegrationTestJson.Options);
        Assert.NotNull(job);

        var key = Guid.NewGuid();
        var firstResponse = await authedClient.SendAsync(TestRunJobHelper.CreateRunRequest(job.Id, key));
        Assert.Equal(HttpStatusCode.Accepted, firstResponse.StatusCode);

        var secondResponse = await authedClient.SendAsync(TestRunJobHelper.CreateRunRequest(job.Id, key));
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);

        var first = await firstResponse.Content.ReadFromJsonAsync<RunJobResponse>(IntegrationTestJson.Options);
        var second = await secondResponse.Content.ReadFromJsonAsync<RunJobResponse>(IntegrationTestJson.Options);

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(first.Id, second.Id);
        Assert.True(first.IsNewlyCreated);
        Assert.False(second.IsNewlyCreated);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await db.OutboxMessages.CountAsync(m =>
            m.ExecutionId == first.Id && m.MessageType == OutboxMessageTypes.ExecutionEnqueue));

        await OutboxTestHelper.DispatchAllAsync(_factory);
        Assert.Equal(1, _enqueuer.EnqueuedExecutionIds.Count(id => id == first.Id));
    }

    [Fact]
    public async Task RunJob_MissingIdempotencyKey_Returns400()
    {
        var client = _factory.CreateClient();
        var (authedClient, _, _) = await TestAuthHelper.RegisterUserAsync(_factory);

        var createResponse = await authedClient.PostAsJsonAsync("/api/jobs", new
        {
            name = "No Key Job",
            url = "https://example.com",
            httpMethod = "GET",
            timeoutSeconds = 30
        });

        var job = await createResponse.Content.ReadFromJsonAsync<JobAutomation.Application.DTOs.Jobs.JobResponse>(IntegrationTestJson.Options);
        Assert.NotNull(job);

        var response = await authedClient.PostAsync($"/api/jobs/{job.Id}/run", null);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task RunJob_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.SendAsync(TestRunJobHelper.CreateRunRequest(Guid.NewGuid()));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RunJob_OtherUsersJob_Returns404()
    {
        var client = _factory.CreateClient();
        var (ownerClient, _, _) = await TestAuthHelper.RegisterUserAsync(_factory, "run-owner");
        var (otherClient, _, _) = await TestAuthHelper.RegisterUserAsync(_factory, "run-other");

        var createResponse = await ownerClient.PostAsJsonAsync("/api/jobs", new
        {
            name = "Private",
            url = "https://example.com",
            httpMethod = "GET",
            timeoutSeconds = 30
        });

        var job = await createResponse.Content.ReadFromJsonAsync<JobAutomation.Application.DTOs.Jobs.JobResponse>(IntegrationTestJson.Options);
        Assert.NotNull(job);

        var response = await otherClient.SendAsync(TestRunJobHelper.CreateRunRequest(job.Id));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RunJob_PausedJob_Returns409()
    {
        var client = _factory.CreateClient();
        var (authedClient, _, _) = await TestAuthHelper.RegisterUserAsync(_factory);

        var createResponse = await authedClient.PostAsJsonAsync("/api/jobs", new
        {
            name = "Paused Job",
            url = "https://example.com",
            httpMethod = "GET",
            timeoutSeconds = 30
        });

        var job = await createResponse.Content.ReadFromJsonAsync<JobAutomation.Application.DTOs.Jobs.JobResponse>(IntegrationTestJson.Options);
        Assert.NotNull(job);

        await authedClient.PostAsync($"/api/jobs/{job.Id}/disable", null);

        var response = await authedClient.SendAsync(TestRunJobHelper.CreateRunRequest(job.Id));
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task ListExecutions_ReturnsHistory()
    {
        _enqueuer.EnqueuedExecutionIds.Clear();
        var client = _factory.CreateClient();
        var (authedClient, _, _) = await TestAuthHelper.RegisterUserAsync(_factory);

        var createResponse = await authedClient.PostAsJsonAsync("/api/jobs", new
        {
            name = "History Job",
            url = "https://example.com",
            httpMethod = "GET",
            timeoutSeconds = 30
        });

        var job = await createResponse.Content.ReadFromJsonAsync<JobAutomation.Application.DTOs.Jobs.JobResponse>(IntegrationTestJson.Options);
        Assert.NotNull(job);

        await authedClient.SendAsync(TestRunJobHelper.CreateRunRequest(job.Id));

        var listResponse = await authedClient.GetFromJsonAsync<PagedExecutionsResponse>(
            $"/api/jobs/{job.Id}/executions",
            IntegrationTestJson.Options);

        Assert.NotNull(listResponse);
        Assert.NotEmpty(listResponse.Items);
    }
}
