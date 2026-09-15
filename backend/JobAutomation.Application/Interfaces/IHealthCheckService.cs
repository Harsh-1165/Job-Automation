using JobAutomation.Application.DTOs;
using JobAutomation.Application.DTOs.Health;

namespace JobAutomation.Application.Interfaces;

public interface IHealthCheckService
{
    Task<HealthResponse> GetHealthAsync(CancellationToken cancellationToken = default);

    HealthLiveResponse GetLiveness();

    Task<HealthReadyResponse> GetReadinessAsync(CancellationToken cancellationToken = default);

    Task<bool> IsDatabaseHealthyAsync(CancellationToken cancellationToken = default);

    Task<bool> IsHangfireHealthyAsync(CancellationToken cancellationToken = default);
}
