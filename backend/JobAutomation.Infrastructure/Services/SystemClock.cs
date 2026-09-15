using JobAutomation.Application.Interfaces;

namespace JobAutomation.Infrastructure.Services;

public sealed class SystemClock : ISystemClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
