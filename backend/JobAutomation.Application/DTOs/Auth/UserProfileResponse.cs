namespace JobAutomation.Application.DTOs.Auth;

public sealed class UserProfileResponse
{
    public required Guid UserId { get; init; }

    public required string Email { get; init; }

    public required bool IsAdmin { get; init; }
}
