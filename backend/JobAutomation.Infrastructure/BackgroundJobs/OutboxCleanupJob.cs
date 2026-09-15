using JobAutomation.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace JobAutomation.Infrastructure.BackgroundJobs;

public class OutboxCleanupJob
{
    private readonly IOutboxDispatcher _dispatcher;
    private readonly ILogger<OutboxCleanupJob> _logger;

    public OutboxCleanupJob(IOutboxDispatcher dispatcher, ILogger<OutboxCleanupJob> logger)
    {
        _dispatcher = dispatcher;
        _logger = logger;
    }

    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var removed = await _dispatcher.CleanupProcessedAsync(cancellationToken);

        if (removed > 0)
        {
            _logger.LogInformation("OutboxCleanupJob removed {Count} processed messages", removed);
        }
    }
}
