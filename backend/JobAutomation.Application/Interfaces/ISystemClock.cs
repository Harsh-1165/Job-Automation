namespace JobAutomation.Application.Interfaces;

public interface ISystemClock
{
    DateTime UtcNow { get; }
}
