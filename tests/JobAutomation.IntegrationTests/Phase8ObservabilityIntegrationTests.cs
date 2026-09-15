using System.Net;
using System.Net.Http.Json;
using JobAutomation.Application.DTOs.Admin;
using JobAutomation.Application.DTOs.Dashboard;
using JobAutomation.Application.DTOs.Health;
using JobAutomation.Domain.Enums;
using JobAutomation.Infrastructure.Persistence;
using JobAutomation.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JobAutomation.IntegrationTests;

[Collection("IntegrationTests")]
public class Phase8ObservabilityIntegrationTests
{
    private readonly ApiWebApplicationFactory _factory;

    public Phase8ObservabilityIntegrationTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task HealthLive_AlwaysReturnsHealthy()
    {
        var response = await _factory.CreateClient().GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<HealthLiveResponse>(IntegrationTestJson.Options);
        Assert.NotNull(body);
        Assert.Equal("Healthy", body.Status);
    }

    [Fact]
    public async Task HealthReady_ReturnsDependencyStatus()
    {
        var response = await _factory.CreateClient().GetAsync("/health/ready");
        Assert.True(response.StatusCode is HttpStatusCode.OK or HttpStatusCode.ServiceUnavailable);

        var body = await response.Content.ReadFromJsonAsync<HealthReadyResponse>(IntegrationTestJson.Options);
        Assert.NotNull(body);
        Assert.NotNull(body.Status);
    }

    [Fact]
    public async Task CorrelationId_GeneratedWhenMissing()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/health/live");

        Assert.True(response.Headers.TryGetValues("X-Request-Id", out var values));
        Assert.True(Guid.TryParse(values!.First(), out _));
    }

    [Fact]
    public async Task CorrelationId_ReusedWhenProvided()
    {
        var client = _factory.CreateClient();
        var requestId = Guid.NewGuid().ToString("D");
        var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("X-Request-Id", requestId);

        var response = await client.SendAsync(request);
        Assert.True(response.Headers.TryGetValues("X-Request-Id", out var values));
        Assert.Equal(requestId, values!.First());
    }

    [Fact]
    public async Task DashboardSummary_ReturnsUserScopedMetrics()
    {
        var (clientA, _, emailA) = await TestAuthHelper.RegisterUserAsync(_factory, "dash-a");
        var (clientB, _, _) = await TestAuthHelper.RegisterUserAsync(_factory, "dash-b");

        var userIdA = await GetUserIdAsync(emailA);
        await SeedExecutionAsync(userIdA, ExecutionStatus.Succeeded);
        await SeedExecutionAsync(userIdA, ExecutionStatus.Failed);

        var summaryA = await clientA.GetFromJsonAsync<DashboardSummaryResponse>(
            "/api/dashboard/summary?range=24h",
            IntegrationTestJson.Options);

        Assert.NotNull(summaryA);
        Assert.True(summaryA.Executions.Total >= 2);
        Assert.True(summaryA.Jobs.Total >= 1);

        var summaryB = await clientB.GetFromJsonAsync<DashboardSummaryResponse>(
            "/api/dashboard/summary?range=24h",
            IntegrationTestJson.Options);

        Assert.NotNull(summaryB);
        Assert.Equal(0, summaryB.Executions.Total);
    }

    [Fact]
    public async Task AdminSystemHealth_ForbiddenForNormalUser()
    {
        var (client, _, _) = await TestAuthHelper.RegisterUserAsync(_factory, "not-admin");
        var response = await client.GetAsync("/api/admin/system");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminSystemHealth_ReturnsForAdmin()
    {
        var (client, _) = await AdminTestHelper.RegisterAdminAsync(_factory, "admin-user");
        var response = await client.GetAsync("/api/admin/system");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<SystemHealthResponse>(IntegrationTestJson.Options);
        Assert.NotNull(body);
        Assert.NotNull(body.Outbox);
        Assert.NotNull(body.Workers);
    }

    [Fact]
    public async Task DashboardSummary_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/dashboard/summary");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<Guid> GetUserIdAsync(string email)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await db.Users.Where(u => u.Email == email).Select(u => u.Id).FirstAsync();
    }

    private async Task SeedExecutionAsync(Guid userId, ExecutionStatus status)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = DateTime.UtcNow;
        var jobId = Guid.NewGuid();

        if (!await db.Jobs.AnyAsync(j => j.UserId == userId))
        {
            db.Jobs.Add(new Domain.Entities.Job
            {
                Id = jobId,
                UserId = userId,
                Name = "Dashboard Job",
                HttpMethod = "GET",
                TargetUrl = "https://example.com",
                Status = JobStatus.Active,
                TimeoutSeconds = 30,
                CreatedAtUtc = now,
                UpdatedAtUtc = now
            });
        }
        else
        {
            jobId = await db.Jobs.Where(j => j.UserId == userId).Select(j => j.Id).FirstAsync();
        }

        db.Executions.Add(new Domain.Entities.Execution
        {
            Id = Guid.NewGuid(),
            JobId = jobId,
            Status = status,
            TriggerType = TriggerType.Manual,
            IdempotencyKey = Guid.NewGuid().ToString(),
            AttemptNumber = 1,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            StartedAtUtc = now.AddMinutes(-1),
            CompletedAtUtc = status is ExecutionStatus.Succeeded or ExecutionStatus.Failed ? now : null
        });

        await db.SaveChangesAsync();
    }
}
