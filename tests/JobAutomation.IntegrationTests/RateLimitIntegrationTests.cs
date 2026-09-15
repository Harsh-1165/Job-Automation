using System.Net;
using System.Net.Http.Json;
using JobAutomation.Application.Interfaces;
using JobAutomation.Infrastructure.Persistence;
using JobAutomation.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JobAutomation.IntegrationTests;

public class RateLimitIntegrationTests : IClassFixture<RateLimitWebApplicationFactory>
{
    private readonly RateLimitWebApplicationFactory _factory;

    public RateLimitIntegrationTests(RateLimitWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Login_ExceedsLimit_Returns429()
    {
        var client = _factory.CreateClient();
        var email = $"rate-{Guid.NewGuid():N}@example.com";

        var first = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "wrong" });
        var second = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "wrong" });
        var limited = await client.PostAsJsonAsync("/api/auth/login", new { email, password = "wrong" });

        Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
    }
}

public class RateLimitWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _connectionString =
        Environment.GetEnvironmentVariable("TEST_DATABASE_CONNECTION_STRING")
        ?? "Host=127.0.0.1;Port=5433;Database=jobautomation_test;Username=jobautomation;Password=change_me_in_local_env";

    public FakeBackgroundJobEnqueuer Enqueuer { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("RateLimitTesting");
        builder.UseSetting("DATABASE_CONNECTION_STRING", _connectionString);
        builder.UseSetting("ConnectionStrings:DefaultConnection", _connectionString);
        builder.UseSetting("JWT_SECRET", "integration-test-secret-key-min-32-chars-long!");
        builder.UseSetting("JWT_ISSUER", "JobAutomation");
        builder.UseSetting("JWT_AUDIENCE", "JobAutomation");
        builder.UseSetting("RateLimiting:AuthPermitLimit", "2");
        builder.UseSetting("RateLimiting:AuthWindowSeconds", "60");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _connectionString,
                ["DATABASE_CONNECTION_STRING"] = _connectionString,
                ["JWT_SECRET"] = "integration-test-secret-key-min-32-chars-long!",
                ["JWT_ISSUER"] = "JobAutomation",
                ["JWT_AUDIENCE"] = "JobAutomation",
                ["RateLimiting:EnableInTesting"] = "true",
                ["RateLimiting:AuthPermitLimit"] = "2",
                ["RateLimiting:AuthWindowSeconds"] = "60"
            });
        });

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (descriptor is not null)
            {
                services.Remove(descriptor);
            }

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(_connectionString));

            services.AddSingleton<IBackgroundJobEnqueuer>(Enqueuer);
            services.AddSingleton<IOutboxPublisher>(sp => new FakeOutboxPublisher(Enqueuer));
            services.AddSingleton<IJobScheduler, FakeJobScheduler>();
        });
    }

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;
}
