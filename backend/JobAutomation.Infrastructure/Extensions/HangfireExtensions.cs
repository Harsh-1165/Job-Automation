using Hangfire;
using Hangfire.PostgreSql;
using JobAutomation.Infrastructure.BackgroundJobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using JobAutomation.Infrastructure.Scheduling;

namespace JobAutomation.Infrastructure.Extensions;

public static class HangfireExtensions
{
    public const string DefaultQueue = "default";

    public static IServiceCollection AddHangfireInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = DependencyInjectionExtensions.GetConnectionString(configuration);

        services.AddHangfire(config =>
        {
            config
                .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                .UseSimpleAssemblyNameTypeSerializer()
                .UseRecommendedSerializerSettings()
                .UsePostgreSqlStorage(options =>
                    options.UseNpgsqlConnection(connectionString));
        });

        return services;
    }

    public static IServiceCollection AddHangfireApiServer(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHangfireServer(options =>
        {
            options.ServerName = $"api-{Environment.MachineName}-{Guid.NewGuid():N}";
            options.Queues = [DefaultQueue];
            options.WorkerCount = 1;
        });

        return services;
    }

    public static IServiceCollection AddHangfireWorkerServer(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var workerCount = configuration.GetValue("Hangfire:WorkerCount", 2);

        services.AddHangfireServer(options =>
        {
            options.ServerName = $"worker-{Environment.MachineName}-{Guid.NewGuid():N}";
            options.Queues = [DefaultQueue];
            options.WorkerCount = workerCount;
        });

        return services;
    }

    public static async Task RegisterRecurringJobsAsync(IHost host)
    {
        var recurringJobManager = host.Services.GetRequiredService<IRecurringJobManager>();

        recurringJobManager.AddOrUpdate<WorkerHealthJob>(
            "worker-health-check",
            job => job.ExecuteAsync(CancellationToken.None),
            Cron.Minutely);

        recurringJobManager.AddOrUpdate<ExecutionRecoveryJob>(
            "execution-stale-recovery",
            job => job.ExecuteAsync(CancellationToken.None),
            Cron.Minutely);

        recurringJobManager.AddOrUpdate<OutboxCleanupJob>(
            "outbox-cleanup",
            job => job.ExecuteAsync(CancellationToken.None),
            Cron.Daily);

        recurringJobManager.AddOrUpdate<RetryRecoveryJob>(
            "retry-recovery",
            job => job.ExecuteAsync(CancellationToken.None),
            Cron.Minutely);

        using var scope = host.Services.CreateScope();
        var syncService = scope.ServiceProvider.GetRequiredService<JobScheduleSyncService>();
        await syncService.SyncActiveJobSchedulesAsync();
    }
}
