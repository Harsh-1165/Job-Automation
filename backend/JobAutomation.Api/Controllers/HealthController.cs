using JobAutomation.Application.DTOs;
using JobAutomation.Application.DTOs.Health;
using JobAutomation.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace JobAutomation.Api.Controllers;

[ApiController]
[Route("health")]
public class HealthController : ControllerBase
{
    private readonly IHealthCheckService _healthCheckService;

    public HealthController(IHealthCheckService healthCheckService)
    {
        _healthCheckService = healthCheckService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var response = await _healthCheckService.GetHealthAsync(cancellationToken);

        if (string.Equals(response.Status, "Healthy", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(response);
        }

        return StatusCode(StatusCodes.Status503ServiceUnavailable, response);
    }

    [HttpGet("live")]
    [ProducesResponseType(typeof(HealthLiveResponse), StatusCodes.Status200OK)]
    public IActionResult GetLive()
    {
        return Ok(_healthCheckService.GetLiveness());
    }

    [HttpGet("ready")]
    [ProducesResponseType(typeof(HealthReadyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HealthReadyResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetReady(CancellationToken cancellationToken)
    {
        var response = await _healthCheckService.GetReadinessAsync(cancellationToken);

        if (string.Equals(response.Status, "Ready", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(response);
        }

        return StatusCode(StatusCodes.Status503ServiceUnavailable, response);
    }
}
