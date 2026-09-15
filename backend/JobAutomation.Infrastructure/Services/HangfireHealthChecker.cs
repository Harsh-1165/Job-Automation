using Hangfire;
using Microsoft.Extensions.Logging;

namespace JobAutomation.Infrastructure.Services;

public class HangfireHealthChecker
{
    private readonly JobStorage? _jobStorage;
    private readonly ILogger<HangfireHealthChecker> _logger;

    public HangfireHealthChecker(JobStorage jobStorage, ILogger<HangfireHealthChecker> logger)
    {
        _jobStorage = jobStorage;
        _logger = logger;
    }

    public bool IsHealthy()
    {
        try
        {
            if (_jobStorage is null)
            {
                return false;
            }

            _ = _jobStorage.GetMonitoringApi().GetStatistics();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Hangfire health check failed");
            return false;
        }
    }
}
