namespace JobAutomation.Application.DTOs.Jobs;

public sealed class CreateJobRequest
{
    public required string Name { get; init; }

    public string? Description { get; init; }

    public required string Url { get; init; }

    public required string HttpMethod { get; init; }

    public Dictionary<string, string>? Headers { get; init; }

    public string? Body { get; init; }

    public string? Schedule { get; init; }

    public int TimeoutSeconds { get; init; } = 30;
}
