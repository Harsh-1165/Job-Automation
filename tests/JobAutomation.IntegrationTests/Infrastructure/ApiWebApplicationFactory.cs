using JobAutomation.Application.Interfaces;
using JobAutomation.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace JobAutomation.IntegrationTests.Infrastructure;

public class ApiWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly string _connectionString;

    public FakeBackgroundJobEnqueuer Enqueuer { get; } = new();

    public FakeJobScheduler JobScheduler { get; } = new();

    public ApiWebApplicationFactory()
    {
        _connectionString = Environment.GetEnvironmentVariable("TEST_DATABASE_CONNECTION_STRING")
            ?? "Host=127.0.0.1;Port=5433;Database=jobautomation_test;Username=jobautomation;Password=change_me_in_local_env";
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _connectionString,
                ["DATABASE_CONNECTION_STRING"] = _connectionString
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

            var enqueuerDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IBackgroundJobEnqueuer));

            if (enqueuerDescriptor is not null)
            {
                services.Remove(enqueuerDescriptor);
            }

            services.AddSingleton<IBackgroundJobEnqueuer>(Enqueuer);
            services.AddSingleton<IOutboxPublisher>(sp => new FakeOutboxPublisher(Enqueuer));

            var schedulerDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(IJobScheduler));

            if (schedulerDescriptor is not null)
            {
                services.Remove(schedulerDescriptor);
            }

            services.AddSingleton<IJobScheduler>(JobScheduler);
        });

        builder.UseSetting("JWT_SECRET", "integration-test-secret-key-min-32-chars-long!");
        builder.UseSetting("JWT_ISSUER", "JobAutomation");
        builder.UseSetting("JWT_AUDIENCE", "JobAutomation");
        builder.UseSetting("ConnectionStrings:DefaultConnection", _connectionString);
        builder.UseSetting("DATABASE_CONNECTION_STRING", _connectionString);
    }

    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;
}
