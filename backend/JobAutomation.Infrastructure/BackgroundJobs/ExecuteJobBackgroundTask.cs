using JobAutomation.Application.Interfaces;
using JobAutomation.Infrastructure.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace JobAutomation.Infrastructure.BackgroundJobs;

public class ExecuteJobBackgroundTask
{
    private readonly IExecutionService _executionService;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ExecuteJobBackgroundTask> _logger;

    public ExecuteJobBackgroundTask(
        IExecutionService executionService,
        IServiceProvider serviceProvider,
        ILogger<ExecuteJobBackgroundTask> logger)
    {
        _executionService = executionService;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task ExecuteAsync(Guid executionId, CancellationToken cancellationToken = default)
    {
        var workerId = _serviceProvider.GetService<WorkerIdentity>()?.WorkerId;

        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["ExecutionId"] = executionId,
            ["WorkerId"] = workerId ?? "unknown"
        }))
        {
            try
            {
                await RecordProcessingAsync(cancellationToken);
                await _executionService.ProcessExecutionAsync(executionId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error processing execution {ExecutionId}", executionId);
            }
        }
    }

    public async Task PrepareRetryAsync(Guid executionId, CancellationToken cancellationToken = default)
    {
        var workerId = _serviceProvider.GetService<WorkerIdentity>()?.WorkerId;

        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["ExecutionId"] = executionId,
            ["WorkerId"] = workerId ?? "unknown"
        }))
        {
            try
            {
                await RecordProcessingAsync(cancellationToken);
                await _executionService.PrepareRetryAsync(executionId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unhandled error preparing retry for execution {ExecutionId}", executionId);
            }
        }
    }

    private async Task RecordProcessingAsync(CancellationToken cancellationToken)
    {
        var heartbeat = _serviceProvider.GetService<IWorkerHeartbeatService>();
        if (heartbeat is not null)
        {
            await heartbeat.RecordProcessingAsync(cancellationToken);
        }
    }
}
