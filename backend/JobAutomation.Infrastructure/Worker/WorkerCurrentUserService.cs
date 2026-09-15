using JobAutomation.Application.Interfaces;

namespace JobAutomation.Infrastructure.Worker;

/// <summary>
/// Worker background jobs have no HTTP user context. API-facing user operations are not available.
/// </summary>
public class WorkerCurrentUserService : ICurrentUserService
{
    public Guid? UserId => null;

    public string? Email => null;

    public bool IsAuthenticated => false;

    public bool IsAdmin => false;

    public Guid GetRequiredUserId()
    {
        throw new InvalidOperationException("Worker processes do not have an authenticated user context.");
    }

    public void RequireAdmin()
    {
        throw new InvalidOperationException("Worker processes do not have an authenticated user context.");
    }
}
