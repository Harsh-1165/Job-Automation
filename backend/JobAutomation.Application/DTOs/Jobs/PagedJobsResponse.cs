namespace JobAutomation.Application.DTOs.Jobs;

public sealed class PagedJobsResponse
{
    public required IReadOnlyList<JobResponse> Items { get; init; }

    public required int Page { get; init; }

    public required int PageSize { get; init; }

    public required int TotalCount { get; init; }
}
