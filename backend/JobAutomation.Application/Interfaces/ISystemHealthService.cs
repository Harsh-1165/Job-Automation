using JobAutomation.Application.DTOs.Admin;

namespace JobAutomation.Application.Interfaces;

public interface ISystemHealthService
{
    Task<SystemHealthResponse> GetSystemHealthAsync(CancellationToken cancellationToken = default);
}
