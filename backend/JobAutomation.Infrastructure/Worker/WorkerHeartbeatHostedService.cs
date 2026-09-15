using JobAutomation.Application;
using JobAutomation.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace JobAutomation.Infrastructure.Worker;

public class WorkerHeartbeatHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ObservabilityOptions _options;
    private readonly ILogger<WorkerHeartbeatHostedService> _logger;

    public WorkerHeartbeatHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<ObservabilityOptions> options,
        ILogger<WorkerHeartbeatHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(5, _options.WorkerHeartbeatIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var heartbeat = scope.ServiceProvider.GetRequiredService<IWorkerHeartbeatService>();
                await heartbeat.RecordHeartbeatAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Worker heartbeat update failed");
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
