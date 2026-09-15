using JobAutomation.Application;
using JobAutomation.Application.DTOs.Dashboard;
using JobAutomation.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobAutomation.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _dashboardService;

    public DashboardController(IDashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet("summary")]
    [ProducesResponseType(typeof(DashboardSummaryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetSummary(
        [FromQuery] string? range,
        CancellationToken cancellationToken)
    {
        DashboardTimeRange parsed;

        try
        {
            parsed = DashboardTimeRangeParser.Parse(range);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { error = ex.Message });
        }

        var summary = await _dashboardService.GetSummaryAsync(parsed, cancellationToken);
        return Ok(summary);
    }
}
