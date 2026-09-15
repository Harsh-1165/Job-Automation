using JobAutomation.Application.DTOs.Jobs;

using JobAutomation.Application.Interfaces;

using JobAutomation.Application.Mapping;

using JobAutomation.Application.Validation;

using JobAutomation.Domain;

using JobAutomation.Domain.Entities;

using JobAutomation.Domain.Enums;

using JobAutomation.Domain.Exceptions;

using Microsoft.EntityFrameworkCore;

using Microsoft.Extensions.Logging;



namespace JobAutomation.Infrastructure.Services;



public class JobService : IJobService

{

    private const int MaxPageSize = 100;



    private readonly IApplicationDbContext _dbContext;

    private readonly ICurrentUserService _currentUser;

    private readonly ICronScheduleValidator _cronValidator;

    private readonly IJobScheduler _jobScheduler;

    private readonly ISsrTargetValidator _ssrTargetValidator;

    private readonly ILogger<JobService> _logger;



    public JobService(

        IApplicationDbContext dbContext,

        ICurrentUserService currentUser,

        ICronScheduleValidator cronValidator,

        IJobScheduler jobScheduler,

        ISsrTargetValidator ssrTargetValidator,

        ILogger<JobService> logger)

    {

        _dbContext = dbContext;

        _currentUser = currentUser;

        _cronValidator = cronValidator;

        _jobScheduler = jobScheduler;

        _ssrTargetValidator = ssrTargetValidator;

        _logger = logger;

    }



    public async Task<JobResponse> CreateAsync(

        CreateJobRequest request,

        CancellationToken cancellationToken = default)

    {

        JobRequestValidator.ValidateCreate(request);

        _cronValidator.Validate(request.Schedule);

        await ValidateTargetUrlAsync(request.Url.Trim(), cancellationToken);



        var userId = _currentUser.GetRequiredUserId();

        var now = DateTime.UtcNow;



        var job = new Job

        {

            Id = Guid.NewGuid(),

            UserId = userId,

            Name = request.Name.Trim(),

            Description = request.Description?.Trim(),

            TargetUrl = request.Url.Trim(),

            HttpMethod = request.HttpMethod.ToUpperInvariant(),

            RequestHeadersJson = JobMapper.SerializeHeaders(request.Headers),

            RequestBody = request.Body,

            CronExpression = NormalizeSchedule(request.Schedule),

            Status = JobStatus.Active,

            TimeoutSeconds = request.TimeoutSeconds,

            CreatedAtUtc = now,

            UpdatedAtUtc = now

        };



        job.NextRunAtUtc = _cronValidator.GetNextOccurrenceUtc(job.CronExpression, now);



        _dbContext.Jobs.Add(job);

        await _dbContext.SaveChangesAsync(cancellationToken);



        await SyncScheduleAsync(job, cancellationToken);



        _logger.LogInformation("Job {JobId} created by user {UserId}", job.Id, userId);



        return JobMapper.ToResponse(job);

    }



    public async Task<PagedJobsResponse> ListAsync(

        int page,

        int pageSize,

        string? search,

        JobStatus? status,

        bool includeArchived,

        CancellationToken cancellationToken = default)

    {

        var userId = _currentUser.GetRequiredUserId();



        page = Math.Max(1, page);

        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);



        var query = _dbContext.Jobs

            .AsNoTracking()

            .Where(j => j.UserId == userId);



        if (!includeArchived)

        {

            query = query.Where(j => j.Status != JobStatus.Archived);

        }



        if (status.HasValue)

        {

            query = query.Where(j => j.Status == status.Value);

        }



        if (!string.IsNullOrWhiteSpace(search))

        {

            var term = search.Trim();

            query = query.Where(j => EF.Functions.ILike(j.Name, $"%{term}%"));

        }



        var totalCount = await query.CountAsync(cancellationToken);



        var items = await query

            .OrderByDescending(j => j.UpdatedAtUtc)

            .Skip((page - 1) * pageSize)

            .Take(pageSize)

            .ToListAsync(cancellationToken);



        return new PagedJobsResponse

        {

            Items = items.Select(JobMapper.ToResponse).ToList(),

            Page = page,

            PageSize = pageSize,

            TotalCount = totalCount

        };

    }



    public async Task<JobResponse> GetByIdAsync(Guid jobId, CancellationToken cancellationToken = default)

    {

        var job = await GetOwnedJobAsync(jobId, cancellationToken);

        return JobMapper.ToResponse(job);

    }



    public async Task<JobResponse> UpdateAsync(

        Guid jobId,

        UpdateJobRequest request,

        CancellationToken cancellationToken = default)

    {

        JobRequestValidator.ValidateUpdate(request);

        _cronValidator.Validate(request.Schedule);

        await ValidateTargetUrlAsync(request.Url.Trim(), cancellationToken);



        var job = await GetOwnedJobForUpdateAsync(jobId, cancellationToken);

        JobStatusTransitions.EnsureEditable(job);



        job.Name = request.Name.Trim();

        job.Description = request.Description?.Trim();

        job.TargetUrl = request.Url.Trim();

        job.HttpMethod = request.HttpMethod.ToUpperInvariant();

        job.RequestHeadersJson = JobMapper.SerializeHeaders(request.Headers);

        job.RequestBody = request.Body;

        job.CronExpression = NormalizeSchedule(request.Schedule);

        job.TimeoutSeconds = request.TimeoutSeconds;

        job.NextRunAtUtc = _cronValidator.GetNextOccurrenceUtc(job.CronExpression, DateTime.UtcNow);

        job.UpdatedAtUtc = DateTime.UtcNow;



        await _dbContext.SaveChangesAsync(cancellationToken);

        await SyncScheduleAsync(job, cancellationToken);



        _logger.LogInformation("Job {JobId} updated by user {UserId}", jobId, _currentUser.UserId);



        return JobMapper.ToResponse(job);

    }



    public async Task ArchiveAsync(Guid jobId, CancellationToken cancellationToken = default)

    {

        var job = await GetOwnedJobForUpdateAsync(jobId, cancellationToken);

        JobStatusTransitions.Archive(job);

        job.NextRunAtUtc = null;

        job.UpdatedAtUtc = DateTime.UtcNow;



        await _dbContext.SaveChangesAsync(cancellationToken);

        await SyncScheduleAsync(job, cancellationToken);



        _logger.LogInformation("Job {JobId} archived by user {UserId}", jobId, _currentUser.UserId);

    }



    public async Task<JobResponse> EnableAsync(Guid jobId, CancellationToken cancellationToken = default)

    {

        var job = await GetOwnedJobForUpdateAsync(jobId, cancellationToken);

        JobStatusTransitions.Enable(job);

        job.NextRunAtUtc = _cronValidator.GetNextOccurrenceUtc(job.CronExpression, DateTime.UtcNow);

        job.UpdatedAtUtc = DateTime.UtcNow;



        await _dbContext.SaveChangesAsync(cancellationToken);

        await SyncScheduleAsync(job, cancellationToken);



        _logger.LogInformation("Job {JobId} enabled by user {UserId}", jobId, _currentUser.UserId);



        return JobMapper.ToResponse(job);

    }



    public async Task<JobResponse> DisableAsync(Guid jobId, CancellationToken cancellationToken = default)

    {

        var job = await GetOwnedJobForUpdateAsync(jobId, cancellationToken);

        JobStatusTransitions.Disable(job);

        job.NextRunAtUtc = null;

        job.UpdatedAtUtc = DateTime.UtcNow;



        await _dbContext.SaveChangesAsync(cancellationToken);

        await SyncScheduleAsync(job, cancellationToken);



        _logger.LogInformation("Job {JobId} disabled by user {UserId}", jobId, _currentUser.UserId);



        return JobMapper.ToResponse(job);

    }



    private async Task SyncScheduleAsync(Job job, CancellationToken cancellationToken)

    {

        if (job.Status == JobStatus.Active && !string.IsNullOrWhiteSpace(job.CronExpression))

        {

            try

            {

                await _jobScheduler.ScheduleRecurringJobAsync(job.Id, job.CronExpression!, cancellationToken);

                job.NextRunAtUtc = _cronValidator.GetNextOccurrenceUtc(job.CronExpression, DateTime.UtcNow);

                await _dbContext.SaveChangesAsync(cancellationToken);

            }

            catch (Exception ex)

            {

                _logger.LogError(

                    ex,

                    "Failed to register recurring schedule for job {JobId}",

                    job.Id);

                throw new DomainException(

                    "SCHEDULE_REGISTRATION_FAILED",

                    "Job was saved but the recurring schedule could not be registered.");

            }

        }

        else

        {

            try

            {

                await _jobScheduler.RemoveRecurringJobAsync(job.Id, cancellationToken);

            }

            catch (Exception ex)

            {

                _logger.LogError(

                    ex,

                    "Failed to remove recurring schedule for job {JobId}",

                    job.Id);

            }

        }

    }



    private static string? NormalizeSchedule(string? schedule)

    {

        if (string.IsNullOrWhiteSpace(schedule))

        {

            return null;

        }



        return schedule.Trim();

    }



    private async Task<Job> GetOwnedJobAsync(Guid jobId, CancellationToken cancellationToken)

    {

        var userId = _currentUser.GetRequiredUserId();



        var job = await _dbContext.Jobs

            .AsNoTracking()

            .FirstOrDefaultAsync(j => j.Id == jobId && j.UserId == userId, cancellationToken);



        if (job is null)

        {

            throw new NotFoundException("Job not found.");

        }



        return job;

    }



    private async Task<Job> GetOwnedJobForUpdateAsync(Guid jobId, CancellationToken cancellationToken)

    {

        var userId = _currentUser.GetRequiredUserId();



        var job = await _dbContext.Jobs

            .FirstOrDefaultAsync(j => j.Id == jobId && j.UserId == userId, cancellationToken);



        if (job is null)

        {

            throw new NotFoundException("Job not found.");

        }



        return job;

    }

    private async Task ValidateTargetUrlAsync(string url, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            throw new ValidationException(
                "Validation failed.",
                new Dictionary<string, string[]>
                {
                    ["url"] = ["URL must be a valid absolute http or https URL."]
                });
        }

        try
        {
            await _ssrTargetValidator.ValidateAsync(uri, cancellationToken);
        }
        catch (SsrValidationException)
        {
            throw new ValidationException(
                "Validation failed.",
                new Dictionary<string, string[]>
                {
                    ["url"] = ["The target URL is not allowed for security reasons."]
                });
        }
    }

}


