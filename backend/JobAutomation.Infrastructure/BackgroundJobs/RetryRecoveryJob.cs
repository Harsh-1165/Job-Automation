using JobAutomation.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace JobAutomation.Infrastructure.BackgroundJobs;

public class RetryRecoveryJob
{
    private readonly IRetryRecoveryService _retryRecoveryService;
    private readonly ILogger<RetryRecoveryJob> _logger;

    public RetryRecoveryJob(
        IRetryRecoveryService retryRecoveryService,
        ILogger<RetryRecoveryJob> logger)
    {
        _retryRecoveryService = retryRecoveryService;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var recovered = await _retryRecoveryService.RecoverOrphanedRetriesAsync(cancellationToken);

        if (recovered > 0)
        {
            _logger.LogInformation("RetryRecoveryJob recovered {Count} orphaned retries", recovered);
        }
    }
}
