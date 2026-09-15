using JobAutomation.Application.DTOs.Dashboard;

namespace JobAutomation.Application.Interfaces;

public interface IDashboardService
{
    Task<DashboardSummaryResponse> GetSummaryAsync(
        DashboardTimeRange range,
        CancellationToken cancellationToken = default);
}
