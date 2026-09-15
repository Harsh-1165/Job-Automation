namespace JobAutomation.Application.Interfaces;

public interface ICronScheduleValidator
{
    void Validate(string? schedule);

    DateTime? GetNextOccurrenceUtc(string? schedule, DateTime fromUtc);
}
