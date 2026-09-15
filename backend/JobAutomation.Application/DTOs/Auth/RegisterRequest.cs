namespace JobAutomation.Application.DTOs.Auth;

public sealed class RegisterRequest
{
    public required string Email { get; init; }

    public required string Password { get; init; }
}
