namespace JobAutomation.Application.DTOs.Executions;

public sealed class PagedExecutionsResponse
{
    public required IReadOnlyList<ExecutionSummaryResponse> Items { get; init; }

    public required int Page { get; init; }

    public required int PageSize { get; init; }

    public required int TotalCount { get; init; }
}
