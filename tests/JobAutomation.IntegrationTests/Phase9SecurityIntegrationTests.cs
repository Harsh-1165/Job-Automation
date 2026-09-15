using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using JobAutomation.Application.DTOs.Jobs;
using JobAutomation.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace JobAutomation.IntegrationTests;

[Collection("IntegrationTests")]
public class Phase9SecurityIntegrationTests
{
    private readonly ApiWebApplicationFactory _factory;

    public Phase9SecurityIntegrationTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CreateJob_LocalhostUrl_Returns422()
    {
        var (client, _, _) = await TestAuthHelper.RegisterUserAsync(_factory, "ssrf");

        var response = await client.PostAsJsonAsync("/api/jobs", new
        {
            name = "Blocked",
            url = "http://127.0.0.1/admin",
            httpMethod = "GET",
            timeoutSeconds = 30
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task CreateJob_AuthorizationHeader_IsRedactedInResponse()
    {
        var (client, _, _) = await TestAuthHelper.RegisterUserAsync(_factory, "redact");

        var response = await client.PostAsJsonAsync("/api/jobs", new
        {
            name = "Secret Header Job",
            url = TestJobUrls.ValidPublic,
            httpMethod = "GET",
            headers = new Dictionary<string, string>
            {
                ["Authorization"] = "Bearer super-secret",
                ["Accept"] = "application/json"
            },
            timeoutSeconds = 30
        });

        response.EnsureSuccessStatusCode();
        var job = await response.Content.ReadFromJsonAsync<JobResponse>(IntegrationTestJson.Options);
        Assert.NotNull(job);
        Assert.Equal("[REDACTED]", job.Headers!["Authorization"]);
        Assert.Equal("application/json", job.Headers["Accept"]);
    }

    [Fact]
    public async Task GetExecution_CrossUser_Returns404()
    {
        var (clientA, _, _) = await TestAuthHelper.RegisterUserAsync(_factory, "exec-a");
        var (clientB, _, _) = await TestAuthHelper.RegisterUserAsync(_factory, "exec-b");

        var job = await CreateJobAsync(clientA);
        var run = await RunJobAsync(clientA, job.Id);
        var response = await clientB.GetAsync($"/api/executions/{run.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task RetryExecution_CrossUser_Returns404()
    {
        var (clientA, _, _) = await TestAuthHelper.RegisterUserAsync(_factory, "retry-a");
        var (clientB, _, _) = await TestAuthHelper.RegisterUserAsync(_factory, "retry-b");

        var job = await CreateJobAsync(clientA);
        var run = await RunJobAsync(clientA, job.Id);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<JobAutomation.Infrastructure.Persistence.ApplicationDbContext>();
        var execution = await db.Executions.FindAsync(run.Id);
        execution!.Status = JobAutomation.Domain.Enums.ExecutionStatus.Failed;
        await db.SaveChangesAsync();

        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/executions/{run.Id}/retry");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        var response = await clientB.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CancelExecution_CrossUser_Returns404()
    {
        var (clientA, _, _) = await TestAuthHelper.RegisterUserAsync(_factory, "cancel-a");
        var (clientB, _, _) = await TestAuthHelper.RegisterUserAsync(_factory, "cancel-b");

        var job = await CreateJobAsync(clientA);
        var run = await RunJobAsync(clientA, job.Id);
        var response = await clientB.PostAsync($"/api/executions/{run.Id}/cancel", null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ListJobExecutions_CrossUser_Returns404()
    {
        var (clientA, _, _) = await TestAuthHelper.RegisterUserAsync(_factory, "list-a");
        var (clientB, _, _) = await TestAuthHelper.RegisterUserAsync(_factory, "list-b");

        var job = await CreateJobAsync(clientA);
        var response = await clientB.GetAsync($"/api/jobs/{job.Id}/executions");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_InvalidJwt_Returns401()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-valid-jwt");
        var response = await client.GetAsync("/api/jobs");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task HealthResponse_IncludesSecurityHeaders()
    {
        var response = await _factory.CreateClient().GetAsync("/health/live");
        response.EnsureSuccessStatusCode();
        Assert.True(response.Headers.Contains("X-Content-Type-Options"));
        Assert.True(response.Headers.Contains("X-Frame-Options"));
        Assert.True(response.Headers.Contains("X-Request-Id"));
    }

    [Fact]
    public async Task ErrorResponse_IncludesRequestId()
    {
        var requestId = Guid.NewGuid().ToString("D");
        var client = _factory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/jobs");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "invalid");
        request.Headers.Add("X-Request-Id", requestId);

        var response = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(requestId, response.Headers.GetValues("X-Request-Id").First());
    }

    private static async Task<JobResponse> CreateJobAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/api/jobs", new
        {
            name = "Security Test Job",
            url = TestJobUrls.ValidPublic,
            httpMethod = "GET",
            timeoutSeconds = 30
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JobResponse>(IntegrationTestJson.Options))!;
    }

    private static async Task<JobAutomation.Application.DTOs.Executions.RunJobResponse> RunJobAsync(
        HttpClient client,
        Guid jobId)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/jobs/{jobId}/run");
        request.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString("D"));
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JobAutomation.Application.DTOs.Executions.RunJobResponse>(
            IntegrationTestJson.Options))!;
    }
}
