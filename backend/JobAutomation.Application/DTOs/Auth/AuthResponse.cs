namespace JobAutomation.Application.DTOs.Auth;

public sealed class AuthResponse
{
    public required string AccessToken { get; init; }

    public required DateTime ExpiresAt { get; init; }
}
