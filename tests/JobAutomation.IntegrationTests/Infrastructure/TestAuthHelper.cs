using System.Net.Http.Headers;
using System.Net.Http.Json;
using JobAutomation.Application.DTOs.Auth;

namespace JobAutomation.IntegrationTests.Infrastructure;

public static class TestAuthHelper
{
    public static async Task<(HttpClient Client, AuthResponse Auth, string Email)> RegisterUserAsync(
        ApiWebApplicationFactory factory,
        string? emailSuffix = null)
    {
        var unique = Guid.NewGuid().ToString("N");
        var email = emailSuffix is null
            ? $"user-{unique}@example.com"
            : $"user-{emailSuffix}-{unique}@example.com";
        const string password = "StrongPassword123!";

        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email,
            password
        });

        response.EnsureSuccessStatusCode();
        var auth = (await response.Content.ReadFromJsonAsync<AuthResponse>(IntegrationTestJson.Options))!;

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", auth.AccessToken);

        return (client, auth, email);
    }

    public static async Task<AuthResponse> LoginAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new { email, password });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<AuthResponse>(IntegrationTestJson.Options))!;
    }
}
