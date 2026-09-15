using JobAutomation.Application.DTOs.Auth;
using JobAutomation.Domain.Entities;

namespace JobAutomation.Application.Interfaces;

public interface ITokenService
{
    AuthResponse GenerateAuthResponse(User user);
}
