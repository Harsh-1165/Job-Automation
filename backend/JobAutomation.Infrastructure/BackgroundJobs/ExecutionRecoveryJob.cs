using JobAutomation.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace JobAutomation.Infrastructure.BackgroundJobs;

public class ExecutionRecoveryJob
{
    private readonly IExecutionService _executionService;
    private readonly ILogger<ExecutionRecoveryJob> _logger;

    public ExecutionRecoveryJob(
        IExecutionService executionService,
        ILogger<ExecutionRecoveryJob> logger)
    {
        _executionService = executionService;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("ExecutionRecoveryJob started");
        await _executionService.RecoverStaleExecutionsAsync(cancellationToken);
    }
}
