namespace JobAutomation.Application.DTOs.Health;

public sealed class HealthReadyResponse
{
    public required string Status { get; init; }

    public required bool Database { get; init; }

    public required bool Hangfire { get; init; }
}
