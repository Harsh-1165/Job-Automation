using JobAutomation.Api.Extensions;
using JobAutomation.Application.DTOs.Executions;
using JobAutomation.Application.DTOs.Jobs;
using JobAutomation.Application.Interfaces;
using JobAutomation.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace JobAutomation.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/jobs")]
public class JobsController : ControllerBase
{
    private readonly IJobService _jobService;
    private readonly IExecutionService _executionService;

    public JobsController(IJobService jobService, IExecutionService executionService)
    {
        _jobService = jobService;
        _executionService = executionService;
    }

    /// <summary>
    /// Create a new job owned by the authenticated user.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(JobResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create(
        [FromBody] CreateJobRequest request,
        CancellationToken cancellationToken)
    {
        var job = await _jobService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = job.Id }, job);
    }

    /// <summary>
    /// List jobs for the authenticated user.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedJobsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] JobStatus? status = null,
        [FromQuery] bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        var result = await _jobService.ListAsync(page, pageSize, search, status, includeArchived, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Get a job by ID (owner only).
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(JobResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var job = await _jobService.GetByIdAsync(id, cancellationToken);
        return Ok(job);
    }

    /// <summary>
    /// Update a job (owner only, not archived).
    /// </summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(JobResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateJobRequest request,
        CancellationToken cancellationToken)
    {
        var job = await _jobService.UpdateAsync(id, request, cancellationToken);
        return Ok(job);
    }

    /// <summary>
    /// Archive a job (soft delete).
    /// </summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Archive(Guid id, CancellationToken cancellationToken)
    {
        await _jobService.ArchiveAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Enable a paused job (Paused → Active).
    /// </summary>
    [HttpPost("{id:guid}/enable")]
    [ProducesResponseType(typeof(JobResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Enable(Guid id, CancellationToken cancellationToken)
    {
        var job = await _jobService.EnableAsync(id, cancellationToken);
        return Ok(job);
    }

    /// <summary>
    /// Disable an active job (Active → Paused).
    /// </summary>
    [HttpPost("{id:guid}/disable")]
    [ProducesResponseType(typeof(JobResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Disable(Guid id, CancellationToken cancellationToken)
    {
        var job = await _jobService.DisableAsync(id, cancellationToken);
        return Ok(job);
    }

    /// <summary>
    /// Manually trigger a job execution (Run Now).
    /// Requires an <c>Idempotency-Key</c> header (GUID). Reusing the same key for the same job
    /// returns the existing execution (200). A new key creates a new execution (202).
    /// </summary>
    /// <param name="id">Job ID.</param>
    /// <param name="idempotencyKey">Client-generated GUID for duplicate request protection.</param>
    [HttpPost("{id:guid}/run")]
    [EnableRateLimiting(RateLimitingExtensions.RunJobPolicy)]
    [ProducesResponseType(typeof(RunJobResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(RunJobResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Run(
        Guid id,
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        CancellationToken cancellationToken)
    {
        var execution = await _executionService.QueueManualExecutionAsync(
            id,
            idempotencyKey,
            cancellationToken);

        if (execution.IsNewlyCreated)
        {
            return AcceptedAtAction(
                nameof(ExecutionsController.GetById),
                "Executions",
                new { id = execution.Id },
                execution);
        }

        return Ok(execution);
    }

    /// <summary>
    /// List execution history for a job (owner only).
    /// </summary>
    [HttpGet("{id:guid}/executions")]
    [ProducesResponseType(typeof(PagedExecutionsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListExecutions(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var result = await _executionService.ListByJobIdAsync(id, page, pageSize, cancellationToken);
        return Ok(result);
    }
}
