using JobAutomation.Domain.Enums;

namespace JobAutomation.Application.DTOs.Jobs;

public sealed class JobResponse
{
    public required Guid Id { get; init; }

    public required string Name { get; init; }

    public string? Description { get; init; }

    public required string Url { get; init; }

    public required string HttpMethod { get; init; }

    public Dictionary<string, string>? Headers { get; init; }

    public string? Body { get; init; }

    public string? Schedule { get; init; }

    public required JobStatus Status { get; init; }

    public required int TimeoutSeconds { get; init; }

    public required DateTime CreatedAt { get; init; }

    public required DateTime UpdatedAt { get; init; }

    public DateTime? LastRunAt { get; init; }

    public DateTime? NextRunAt { get; init; }
}
