using System.Security.Claims;
using JobAutomation.Application.Interfaces;
using JobAutomation.Domain.Exceptions;
using Microsoft.AspNetCore.Http;

namespace JobAutomation.Infrastructure.Services;

public class CurrentUserService : ICurrentUserService
{
    public const string AdminRole = "Admin";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public Guid? UserId
    {
        get
        {
            var sub = _httpContextAccessor.HttpContext?.User
                .FindFirstValue(ClaimTypes.NameIdentifier)
                ?? _httpContextAccessor.HttpContext?.User
                    .FindFirstValue("sub");

            return Guid.TryParse(sub, out var userId) ? userId : null;
        }
    }

    public string? Email =>
        _httpContextAccessor.HttpContext?.User.FindFirstValue(ClaimTypes.Email)
        ?? _httpContextAccessor.HttpContext?.User.FindFirstValue("email");

    public bool IsAuthenticated =>
        _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false;

    public bool IsAdmin =>
        _httpContextAccessor.HttpContext?.User.IsInRole(AdminRole) ?? false;

    public Guid GetRequiredUserId()
    {
        return UserId ?? throw new UnauthorizedException("Authentication is required.");
    }

    public void RequireAdmin()
    {
        if (!IsAuthenticated)
        {
            throw new UnauthorizedException("Authentication is required.");
        }

        if (!IsAdmin)
        {
            throw new ForbiddenException("Administrator access is required.");
        }
    }
}
