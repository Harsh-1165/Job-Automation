using JobAutomation.Application;
using JobAutomation.Application.Interfaces;
using JobAutomation.Infrastructure.Authentication;
using JobAutomation.Infrastructure.BackgroundJobs;
using JobAutomation.Infrastructure.HttpExecution;
using JobAutomation.Infrastructure.Persistence;
using JobAutomation.Infrastructure.Outbox;
using JobAutomation.Infrastructure.Scheduling;
using JobAutomation.Infrastructure.Security;
using JobAutomation.Infrastructure.Services;
using System.Net.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace JobAutomation.Infrastructure.Extensions;

public static class DependencyInjectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var connectionString = GetConnectionString(configuration);

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<IApplicationDbContext>(provider =>
            provider.GetRequiredService<ApplicationDbContext>());

        var jwtSettings = JwtSettingsValidator.Resolve(configuration, environment);
        services.Configure<JwtSettings>(_ =>
        {
            _.Secret = jwtSettings.Secret;
            _.Issuer = jwtSettings.Issuer;
            _.Audience = jwtSettings.Audience;
            _.ExpirationMinutes = jwtSettings.ExpirationMinutes;
        });

        services.AddScoped<IHealthCheckService, HealthCheckService>();
        services.AddSingleton<HangfireHealthChecker>();
        services.AddScoped<IDashboardService, DashboardService>();
        services.AddScoped<ISystemHealthService, SystemHealthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IJobService, JobService>();
        services.AddScoped<ICronScheduleValidator, CronScheduleValidator>();
        services.AddScoped<IJobScheduler, HangfireJobScheduler>();
        services.AddScoped<ScheduledJobBackgroundTask>();
        services.AddScoped<JobScheduleSyncService>();
        services.AddScoped<IExecutionService, ExecutionService>();
        services.AddScoped<IExecutionConcurrencyService, ExecutionConcurrencyService>();
        services.AddScoped<IRetryPolicy, RetryPolicy>();
        services.AddSingleton<ISystemClock, SystemClock>();
        services.AddSingleton<ISsrTargetValidator, SsrTargetValidator>();
        services.AddScoped<IHttpJobExecutor, HttpJobExecutor>();
        services.AddScoped<IBackgroundJobEnqueuer, HangfireBackgroundJobEnqueuer>();
        services.AddScoped<IOutboxWriter, OutboxWriter>();
        services.AddScoped<IOutboxPublisher, HangfireOutboxPublisher>();
        services.AddScoped<IOutboxDispatcher, OutboxDispatcherService>();
        services.AddScoped<IRetryRecoveryService, RetryRecoveryService>();
        services.AddScoped<ExecuteJobBackgroundTask>();
        services.AddScoped<ExecutionRecoveryJob>();
        services.AddScoped<OutboxCleanupJob>();
        services.AddScoped<RetryRecoveryJob>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IPasswordHasher, PasswordHasherService>();
        services.AddScoped<WorkerHealthJob>();

        services.AddOptions<RetryOptions>()
            .Bind(configuration.GetSection(RetryOptions.SectionName))
            .Validate(
                o => o.BaseDelaySeconds > 0,
                "Retry: BaseDelaySeconds must be greater than 0.")
            .Validate(
                o => o.MaxBackoffSeconds >= o.BaseDelaySeconds,
                "Retry: MaxBackoffSeconds must be greater than or equal to BaseDelaySeconds.")
            .Validate(
                o => o.JitterPercentage >= 0 && o.JitterPercentage <= 100,
                "Retry: JitterPercentage must be between 0 and 100.")
            .ValidateOnStart();

        services.AddOptions<ExecutionLeaseOptions>()
            .Bind(configuration.GetSection(ExecutionLeaseOptions.SectionName))
            .Validate(
                o => o.HeartbeatIntervalSeconds > 0
                     && o.LeaseDurationSeconds > 0
                     && o.HeartbeatIntervalSeconds < o.LeaseDurationSeconds,
                "ExecutionLease: HeartbeatIntervalSeconds must be greater than 0 and less than LeaseDurationSeconds.")
            .Validate(
                o => o.RecoveryBatchSize > 0,
                "ExecutionLease: RecoveryBatchSize must be greater than 0.")
            .ValidateOnStart();

        services.AddOptions<OutboxOptions>()
            .Bind(configuration.GetSection(OutboxOptions.SectionName))
            .Validate(o => o.BatchSize > 0, "Outbox: BatchSize must be greater than 0.")
            .Validate(o => o.PollIntervalSeconds > 0, "Outbox: PollIntervalSeconds must be greater than 0.")
            .Validate(o => o.LeaseDurationSeconds > 0, "Outbox: LeaseDurationSeconds must be greater than 0.")
            .ValidateOnStart();

        services.AddOptions<ObservabilityOptions>()
            .Bind(configuration.GetSection(ObservabilityOptions.SectionName))
            .Validate(o => o.WorkerHeartbeatIntervalSeconds > 0, "Observability: WorkerHeartbeatIntervalSeconds must be greater than 0.")
            .Validate(o => o.WorkerStaleThresholdSeconds > 0, "Observability: WorkerStaleThresholdSeconds must be greater than 0.")
            .ValidateOnStart();

        services.AddOptions<RateLimitOptions>()
            .Bind(configuration.GetSection(RateLimitOptions.SectionName));

        services.AddOptions<HttpJobExecutorOptions>()
            .Bind(configuration.GetSection("HttpJobExecutor"));

        services.AddHttpClient("JobExecutor", client =>
        {
            client.Timeout = Timeout.InfiniteTimeSpan;
        }).ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
        {
            AllowAutoRedirect = false
        });

        return services;
    }

    public static string GetConnectionString(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString = configuration["DATABASE_CONNECTION_STRING"];
        }

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Database connection string is not configured. Set ConnectionStrings:DefaultConnection or DATABASE_CONNECTION_STRING.");
        }

        return connectionString;
    }
}
