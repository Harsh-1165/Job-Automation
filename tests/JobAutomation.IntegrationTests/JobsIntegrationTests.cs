using System.Net;
using System.Net.Http.Json;
using JobAutomation.Application.DTOs.Jobs;
using JobAutomation.Domain.Enums;
using JobAutomation.IntegrationTests.Infrastructure;

namespace JobAutomation.IntegrationTests;

[Collection("IntegrationTests")]
public class JobsIntegrationTests
{
    private readonly ApiWebApplicationFactory _factory;

    public JobsIntegrationTests(ApiWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Jobs_Unauthenticated_Returns401()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/jobs");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateJob_AssignsAuthenticatedUserId()
    {
        var client = _factory.CreateClient();
        var (authedClient, _, _) = await TestAuthHelper.RegisterUserAsync(_factory);

        var response = await authedClient.PostAsJsonAsync("/api/jobs", new
        {
            name = "My Job",
            url = TestJobUrls.ValidPublic,
            httpMethod = "GET",
            timeoutSeconds = 30
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var job = await response.Content.ReadFromJsonAsync<JobResponse>(IntegrationTestJson.Options);
        Assert.NotNull(job);
        Assert.Equal(JobStatus.Active, job.Status);
    }

    [Fact]
    public async Task CreateJob_InvalidUrl_Returns422()
    {
        var client = _factory.CreateClient();
        var (authedClient, _, _) = await TestAuthHelper.RegisterUserAsync(_factory);

        var response = await authedClient.PostAsJsonAsync("/api/jobs", new
        {
            name = "Bad URL Job",
            url = "ftp://invalid.example.com",
            httpMethod = "GET",
            timeoutSeconds = 30
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task CreateJob_InvalidMethod_Returns422()
    {
        var client = _factory.CreateClient();
        var (authedClient, _, _) = await TestAuthHelper.RegisterUserAsync(_factory);

        var response = await authedClient.PostAsJsonAsync("/api/jobs", new
        {
            name = "Bad Method",
            url = TestJobUrls.ValidPublic,
            httpMethod = "TRACE",
            timeoutSeconds = 30
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task CreateJob_InvalidTimeout_Returns422()
    {
        var client = _factory.CreateClient();
        var (authedClient, _, _) = await TestAuthHelper.RegisterUserAsync(_factory);

        var response = await authedClient.PostAsJsonAsync("/api/jobs", new
        {
            name = "Bad Timeout",
            url = TestJobUrls.ValidPublic,
            httpMethod = "GET",
            timeoutSeconds = 999
        });

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task ListJobs_ReturnsOnlyOwnJobs()
    {
        var client = _factory.CreateClient();
        var (userAClient, _, _) = await TestAuthHelper.RegisterUserAsync(_factory, "userA");
        var (userBClient, _, _) = await TestAuthHelper.RegisterUserAsync(_factory, "userB");

        await userAClient.PostAsJsonAsync("/api/jobs", new
        {
            name = "User A Job",
            url = TestJobUrls.ValidPublic,
            httpMethod = "GET",
            timeoutSeconds = 30
        });

        await userBClient.PostAsJsonAsync("/api/jobs", new
        {
            name = "User B Job",
            url = "https://example.com/other",
            httpMethod = "GET",
            timeoutSeconds = 30
        });

        var listA = await userAClient.GetFromJsonAsync<PagedJobsResponse>("/api/jobs", IntegrationTestJson.Options);
        Assert.NotNull(listA);
        Assert.All(listA.Items, j => Assert.Contains("User A", j.Name));
        Assert.DoesNotContain(listA.Items, j => j.Name.Contains("User B"));
    }

    [Fact]
    public async Task GetJob_OtherUsersJob_Returns404()
    {
        var client = _factory.CreateClient();
        var (userAClient, _, _) = await TestAuthHelper.RegisterUserAsync(_factory, "owner");
        var (userBClient, _, _) = await TestAuthHelper.RegisterUserAsync(_factory, "other");

        var createResponse = await userAClient.PostAsJsonAsync("/api/jobs", new
        {
            name = "Private Job",
            url = "https://example.com/private",
            httpMethod = "GET",
            timeoutSeconds = 30
        });

        var created = await createResponse.Content.ReadFromJsonAsync<JobResponse>(IntegrationTestJson.Options);
        Assert.NotNull(created);

        var response = await userBClient.GetAsync($"/api/jobs/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ArchiveJob_ExcludedFromDefaultList()
    {
        var client = _factory.CreateClient();
        var (authedClient, _, _) = await TestAuthHelper.RegisterUserAsync(_factory);

        var createResponse = await authedClient.PostAsJsonAsync("/api/jobs", new
        {
            name = "To Archive",
            url = "https://example.com/archive",
            httpMethod = "GET",
            timeoutSeconds = 30
        });

        var created = await createResponse.Content.ReadFromJsonAsync<JobResponse>(IntegrationTestJson.Options);
        Assert.NotNull(created);

        var deleteResponse = await authedClient.DeleteAsync($"/api/jobs/{created.Id}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var list = await authedClient.GetFromJsonAsync<PagedJobsResponse>("/api/jobs", IntegrationTestJson.Options);
        Assert.NotNull(list);
        Assert.DoesNotContain(list.Items, j => j.Id == created.Id);
    }

    [Fact]
    public async Task UpdateArchivedJob_Returns400()
    {
        var client = _factory.CreateClient();
        var (authedClient, _, _) = await TestAuthHelper.RegisterUserAsync(_factory);

        var createResponse = await authedClient.PostAsJsonAsync("/api/jobs", new
        {
            name = "Archive Then Edit",
            url = "https://example.com/edit",
            httpMethod = "GET",
            timeoutSeconds = 30
        });

        var created = await createResponse.Content.ReadFromJsonAsync<JobResponse>(IntegrationTestJson.Options);
        Assert.NotNull(created);

        await authedClient.DeleteAsync($"/api/jobs/{created.Id}");

        var updateResponse = await authedClient.PutAsJsonAsync($"/api/jobs/{created.Id}", new
        {
            name = "Updated",
            url = "https://example.com/updated",
            httpMethod = "GET",
            timeoutSeconds = 30
        });

        Assert.Equal(HttpStatusCode.BadRequest, updateResponse.StatusCode);
    }

    [Fact]
    public async Task EnableDisable_TransitionsWork()
    {
        var client = _factory.CreateClient();
        var (authedClient, _, _) = await TestAuthHelper.RegisterUserAsync(_factory);

        var createResponse = await authedClient.PostAsJsonAsync("/api/jobs", new
        {
            name = "Toggle Job",
            url = "https://example.com/toggle",
            httpMethod = "GET",
            timeoutSeconds = 30
        });

        var created = await createResponse.Content.ReadFromJsonAsync<JobResponse>(IntegrationTestJson.Options);
        Assert.NotNull(created);

        var disableResponse = await authedClient.PostAsync($"/api/jobs/{created.Id}/disable", null);
        Assert.Equal(HttpStatusCode.OK, disableResponse.StatusCode);
        var disabled = await disableResponse.Content.ReadFromJsonAsync<JobResponse>(IntegrationTestJson.Options);
        Assert.Equal(JobStatus.Paused, disabled?.Status);

        var enableResponse = await authedClient.PostAsync($"/api/jobs/{created.Id}/enable", null);
        Assert.Equal(HttpStatusCode.OK, enableResponse.StatusCode);
        var enabled = await enableResponse.Content.ReadFromJsonAsync<JobResponse>(IntegrationTestJson.Options);
        Assert.Equal(JobStatus.Active, enabled?.Status);
    }

    [Fact]
    public async Task UpdateJob_OtherUsersJob_Returns404()
    {
        var client = _factory.CreateClient();
        var (ownerClient, _, _) = await TestAuthHelper.RegisterUserAsync(_factory, "update-owner");
        var (otherClient, _, _) = await TestAuthHelper.RegisterUserAsync(_factory, "update-other");

        var createResponse = await ownerClient.PostAsJsonAsync("/api/jobs", new
        {
            name = "Owner Job",
            url = "https://example.com/owner",
            httpMethod = "GET",
            timeoutSeconds = 30
        });

        var created = await createResponse.Content.ReadFromJsonAsync<JobResponse>(IntegrationTestJson.Options);
        Assert.NotNull(created);

        var response = await otherClient.PutAsJsonAsync($"/api/jobs/{created.Id}", new
        {
            name = "Hacked",
            url = "https://example.com/hacked",
            httpMethod = "GET",
            timeoutSeconds = 30
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ArchiveJob_OtherUsersJob_Returns404()
    {
        var client = _factory.CreateClient();
        var (ownerClient, _, _) = await TestAuthHelper.RegisterUserAsync(_factory, "archive-owner");
        var (otherClient, _, _) = await TestAuthHelper.RegisterUserAsync(_factory, "archive-other");

        var createResponse = await ownerClient.PostAsJsonAsync("/api/jobs", new
        {
            name = "Protected Job",
            url = "https://example.com/protected",
            httpMethod = "GET",
            timeoutSeconds = 30
        });

        var created = await createResponse.Content.ReadFromJsonAsync<JobResponse>(IntegrationTestJson.Options);
        Assert.NotNull(created);

        var response = await otherClient.DeleteAsync($"/api/jobs/{created.Id}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Enable_ActiveJob_Returns400()
    {
        var client = _factory.CreateClient();
        var (authedClient, _, _) = await TestAuthHelper.RegisterUserAsync(_factory);

        var createResponse = await authedClient.PostAsJsonAsync("/api/jobs", new
        {
            name = "Already Active",
            url = "https://example.com/active",
            httpMethod = "GET",
            timeoutSeconds = 30
        });

        var created = await createResponse.Content.ReadFromJsonAsync<JobResponse>(IntegrationTestJson.Options);
        Assert.NotNull(created);

        var response = await authedClient.PostAsync($"/api/jobs/{created.Id}/enable", null);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
