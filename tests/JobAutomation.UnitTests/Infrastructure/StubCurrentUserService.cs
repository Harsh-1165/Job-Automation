using JobAutomation.Application.Interfaces;
using JobAutomation.Domain.Exceptions;

namespace JobAutomation.UnitTests.Infrastructure;

public sealed class StubCurrentUserService : ICurrentUserService
{
    public StubCurrentUserService(Guid userId, bool isAdmin = false)
    {
        UserId = userId;
        IsAdmin = isAdmin;
    }

    public Guid? UserId { get; }

    public string? Email => "test@example.com";

    public bool IsAuthenticated => true;

    public bool IsAdmin { get; }

    public Guid GetRequiredUserId() => UserId!.Value;

    public void RequireAdmin()
    {
        if (!IsAdmin)
        {
            throw new ForbiddenException("Administrator access is required.");
        }
    }
}
