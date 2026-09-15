using System.Net;
using System.Net.Http.Json;
using JobAutomation.Application.DTOs.Auth;
using JobAutomation.IntegrationTests.Infrastructure;

namespace JobAutomation.IntegrationTests;

[Collection("IntegrationTests")]
public class AuthIntegrationTests
{
    private readonly HttpClient _client;

    public AuthIntegrationTests(ApiWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_WithValidData_ReturnsToken()
    {
        var email = $"register-{Guid.NewGuid():N}@example.com";

        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "StrongPassword123!"
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<AuthResponse>(IntegrationTestJson.Options);
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
        Assert.True(body.ExpiresAt > DateTime.UtcNow);

        var raw = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("PasswordHash", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("StrongPassword123!", raw);
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        var email = $"dup-{Guid.NewGuid():N}@example.com";
        var payload = new { email, password = "StrongPassword123!" };

        await _client.PostAsJsonAsync("/api/auth/register", payload);
        var response = await _client.PostAsJsonAsync("/api/auth/register", payload);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Register_DifferentCasing_Returns409()
    {
        var baseEmail = $"case-{Guid.NewGuid():N}@example.com";

        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email = baseEmail.ToLowerInvariant(),
            password = "StrongPassword123!"
        });

        var response = await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email = baseEmail.ToUpperInvariant(),
            password = "StrongPassword123!"
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Login_ValidCredentials_ReturnsToken()
    {
        var email = $"login-{Guid.NewGuid():N}@example.com";
        const string password = "StrongPassword123!";

        await _client.PostAsJsonAsync("/api/auth/register", new { email, password });

        var response = await _client.PostAsJsonAsync("/api/auth/login", new { email, password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AuthResponse>(IntegrationTestJson.Options);
        Assert.NotNull(body?.AccessToken);
    }

    [Fact]
    public async Task Login_InvalidPassword_Returns401()
    {
        var email = $"badlogin-{Guid.NewGuid():N}@example.com";

        await _client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password = "StrongPassword123!"
        });

        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email,
            password = "WrongPassword123!"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
