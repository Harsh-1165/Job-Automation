using JobAutomation.Application.DTOs.Auth;

namespace JobAutomation.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);

    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);

    Task<UserProfileResponse> GetProfileAsync(CancellationToken cancellationToken = default);
}
