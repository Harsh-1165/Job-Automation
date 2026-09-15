using JobAutomation.Api.Extensions;
using JobAutomation.Application.DTOs.Executions;
using JobAutomation.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace JobAutomation.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/executions")]
public class ExecutionsController : ControllerBase
{
    private readonly IExecutionService _executionService;

    public ExecutionsController(IExecutionService executionService)
    {
        _executionService = executionService;
    }

    /// <summary>
    /// Get execution details including logs (owner only).
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ExecutionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var execution = await _executionService.GetByIdAsync(id, cancellationToken);
        return Ok(execution);
    }

    /// <summary>
    /// Manually retry a failed execution (owner only). Requires Idempotency-Key header.
    /// </summary>
    [HttpPost("{id:guid}/retry")]
    [EnableRateLimiting(RateLimitingExtensions.RetryPolicy)]
    [ProducesResponseType(typeof(RunJobResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(RunJobResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Retry(
        Guid id,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var response = await _executionService.RetryExecutionAsync(
            id,
            idempotencyKey ?? string.Empty,
            cancellationToken);

        return response.IsNewlyCreated
            ? Accepted(response)
            : Ok(response);
    }

    /// <summary>
    /// Cancel a queued or retrying execution (owner only).
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(ExecutionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var execution = await _executionService.CancelExecutionAsync(id, cancellationToken);
        return Ok(execution);
    }
}
