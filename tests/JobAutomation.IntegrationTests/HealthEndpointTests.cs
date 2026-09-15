using System.Net;
using System.Net.Http.Json;
using JobAutomation.Application.DTOs;
using JobAutomation.IntegrationTests.Infrastructure;

namespace JobAutomation.IntegrationTests;

[Collection("IntegrationTests")]
public class HealthEndpointTests
{
    private readonly HttpClient _client;

    public HealthEndpointTests(ApiWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task GetHealth_ReturnsJsonResponse()
    {
        var response = await _client.GetAsync("/health");

        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.ServiceUnavailable,
            $"Unexpected status code: {response.StatusCode}");

        var body = await response.Content.ReadFromJsonAsync<HealthResponse>(IntegrationTestJson.Options);
        Assert.NotNull(body);
        Assert.NotNull(body.Status);
    }
}
