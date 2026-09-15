using System.Net;
using System.Net.Http.Json;
using JobAutomation.Application.DTOs.Jobs;
using JobAutomation.IntegrationTests.Infrastructure;

namespace JobAutomation.IntegrationTests;

[Collection("IntegrationTests")]
public class JobSchedulingIntegrationTests
{
    private readonly ApiWebApplicationFactory _factory;
    private readonly FakeJobScheduler _scheduler;

    public JobSchedulingIntegrationTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
        _scheduler = factory.JobScheduler;
    }

    [Fact]
    public async Task CreateJob_ValidSchedule_RegistersRecurringJob()
    {
        _scheduler.ScheduledJobs.Clear();
        var client = _factory.CreateClient();
        var (authedClient, _, _) = await TestAuthHelper.RegisterUserAsync(_factory);

        var response = await authedClient.PostAsJsonAsync("/api/jobs", new
        {
            name = "Scheduled Job",
            url = "https://example.com",
            httpMethod = "GET",
            timeoutSeconds = 30,
            schedule = "0 9 * * *"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var job = await response.Content.ReadFromJsonAsync<JobResponse>(IntegrationTestJson.Options);
        Assert.NotNull(job);
        Assert.Equal("0 9 * * *", job.Schedule);
        Assert.True(_scheduler.ScheduledJobs.ContainsKey(job.Id));
    }

    [Fact]
    public async Task CreateJob_InvalidSchedule_Returns400()
    {
        var client = _factory.CreateClient();
        var (authedClient, _, _) = await TestAuthHelper.RegisterUserAsync(_factory);

        var response = await authedClient.PostAsJsonAsync("/api/jobs", new
        {
            name = "Bad Schedule",
            url = "https://example.com",
            httpMethod = "GET",
            timeoutSeconds = 30,
            schedule = "invalid cron"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task DisableJob_RemovesRecurringSchedule()
    {
        _scheduler.ScheduledJobs.Clear();
        var client = _factory.CreateClient();
        var (authedClient, _, _) = await TestAuthHelper.RegisterUserAsync(_factory);

        var createResponse = await authedClient.PostAsJsonAsync("/api/jobs", new
        {
            name = "Disable Schedule Test",
            url = "https://example.com",
            httpMethod = "GET",
            timeoutSeconds = 30,
            schedule = "*/5 * * * *"
        });

        var job = await createResponse.Content.ReadFromJsonAsync<JobResponse>(IntegrationTestJson.Options);
        Assert.NotNull(job);

        await authedClient.PostAsync($"/api/jobs/{job.Id}/disable", null);

        Assert.DoesNotContain(job.Id, _scheduler.ScheduledJobs.Keys);
        Assert.Contains(job.Id, _scheduler.RemovedJobs);
    }

    [Fact]
    public async Task EnableJob_RegistersRecurringSchedule()
    {
        _scheduler.ScheduledJobs.Clear();
        var client = _factory.CreateClient();
        var (authedClient, _, _) = await TestAuthHelper.RegisterUserAsync(_factory);

        var createResponse = await authedClient.PostAsJsonAsync("/api/jobs", new
        {
            name = "Enable Schedule Test",
            url = "https://example.com",
            httpMethod = "GET",
            timeoutSeconds = 30,
            schedule = "0 * * * *"
        });

        var job = await createResponse.Content.ReadFromJsonAsync<JobResponse>(IntegrationTestJson.Options);
        Assert.NotNull(job);

        await authedClient.PostAsync($"/api/jobs/{job.Id}/disable", null);
        _scheduler.ScheduledJobs.Clear();

        await authedClient.PostAsync($"/api/jobs/{job.Id}/enable", null);

        Assert.True(_scheduler.ScheduledJobs.ContainsKey(job.Id));
    }

    [Fact]
    public async Task UpdateJob_ScheduleChange_UpdatesRecurringSchedule()
    {
        _scheduler.ScheduledJobs.Clear();
        var client = _factory.CreateClient();
        var (authedClient, _, _) = await TestAuthHelper.RegisterUserAsync(_factory);

        var createResponse = await authedClient.PostAsJsonAsync("/api/jobs", new
        {
            name = "Update Schedule Test",
            url = "https://example.com",
            httpMethod = "GET",
            timeoutSeconds = 30,
            schedule = "*/5 * * * *"
        });

        var job = await createResponse.Content.ReadFromJsonAsync<JobResponse>(IntegrationTestJson.Options);
        Assert.NotNull(job);

        await authedClient.PutAsJsonAsync($"/api/jobs/{job.Id}", new
        {
            name = job.Name,
            url = job.Url,
            httpMethod = job.HttpMethod,
            timeoutSeconds = job.TimeoutSeconds,
            schedule = "0 * * * *"
        });

        Assert.Equal("0 * * * *", _scheduler.ScheduledJobs[job.Id]);
    }

    [Fact]
    public async Task ArchiveJob_RemovesRecurringSchedule()
    {
        _scheduler.ScheduledJobs.Clear();
        var client = _factory.CreateClient();
        var (authedClient, _, _) = await TestAuthHelper.RegisterUserAsync(_factory);

        var createResponse = await authedClient.PostAsJsonAsync("/api/jobs", new
        {
            name = "Archive Schedule Test",
            url = "https://example.com",
            httpMethod = "GET",
            timeoutSeconds = 30,
            schedule = "0 9 * * *"
        });

        var job = await createResponse.Content.ReadFromJsonAsync<JobResponse>(IntegrationTestJson.Options);
        Assert.NotNull(job);

        await authedClient.DeleteAsync($"/api/jobs/{job.Id}");

        Assert.DoesNotContain(job.Id, _scheduler.ScheduledJobs.Keys);
        Assert.Contains(job.Id, _scheduler.RemovedJobs);
    }
}
