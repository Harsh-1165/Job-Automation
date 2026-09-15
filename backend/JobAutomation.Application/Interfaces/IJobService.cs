using JobAutomation.Application.DTOs.Jobs;
using JobAutomation.Domain.Enums;

namespace JobAutomation.Application.Interfaces;

public interface IJobService
{
    Task<JobResponse> CreateAsync(CreateJobRequest request, CancellationToken cancellationToken = default);

    Task<PagedJobsResponse> ListAsync(
        int page,
        int pageSize,
        string? search,
        JobStatus? status,
        bool includeArchived,
        CancellationToken cancellationToken = default);

    Task<JobResponse> GetByIdAsync(Guid jobId, CancellationToken cancellationToken = default);

    Task<JobResponse> UpdateAsync(Guid jobId, UpdateJobRequest request, CancellationToken cancellationToken = default);

    Task ArchiveAsync(Guid jobId, CancellationToken cancellationToken = default);

    Task<JobResponse> EnableAsync(Guid jobId, CancellationToken cancellationToken = default);

    Task<JobResponse> DisableAsync(Guid jobId, CancellationToken cancellationToken = default);
}
