using JobAutomation.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace JobAutomation.IntegrationTests.Infrastructure;

public static class AdminTestHelper
{
    public static async Task<(HttpClient Client, string Email)> RegisterAdminAsync(
        ApiWebApplicationFactory factory,
        string? emailSuffix = null)
    {
        var (client, _, email) = await TestAuthHelper.RegisterUserAsync(factory, emailSuffix);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.FirstAsync(u => u.Email == email);
        user.IsAdmin = true;
        await db.SaveChangesAsync();

        var auth = await TestAuthHelper.LoginAsync(client, email, "StrongPassword123!");
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.AccessToken);

        return (client, email);
    }
}
