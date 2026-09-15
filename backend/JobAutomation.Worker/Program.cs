using JobAutomation.Infrastructure.Extensions;
using JobAutomation.Infrastructure.Outbox;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration.AddEnvironmentVariables();

builder.Services.Configure<HostOptions>(options =>
{
    options.ShutdownTimeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddInfrastructure(builder.Configuration, builder.Environment);
builder.Services.AddHangfireInfrastructure(builder.Configuration);
builder.Services.AddHangfireWorkerServer(builder.Configuration);
builder.Services.AddWorkerInfrastructure();
builder.Services.AddHostedService<OutboxDispatcherHostedService>();

var host = builder.Build();

await HangfireExtensions.RegisterRecurringJobsAsync(host);

var logger = host.Services.GetRequiredService<ILogger<Program>>();
logger.LogInformation("Job Automation Worker started");

await host.RunAsync();
