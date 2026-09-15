namespace JobAutomation.Application.DTOs;

public sealed class HealthResponse
{
    public required string Status { get; init; }

    public string? Database { get; init; }

    public string? Hangfire { get; init; }
}
