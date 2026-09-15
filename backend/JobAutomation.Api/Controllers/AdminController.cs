using JobAutomation.Application.DTOs.Admin;
using JobAutomation.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace JobAutomation.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/admin")]
public class AdminController : ControllerBase
{
    private readonly ISystemHealthService _systemHealthService;

    public AdminController(ISystemHealthService systemHealthService)
    {
        _systemHealthService = systemHealthService;
    }

    [HttpGet("system")]
    [ProducesResponseType(typeof(SystemHealthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetSystemHealth(CancellationToken cancellationToken)
    {
        var response = await _systemHealthService.GetSystemHealthAsync(cancellationToken);
        return Ok(response);
    }
}
