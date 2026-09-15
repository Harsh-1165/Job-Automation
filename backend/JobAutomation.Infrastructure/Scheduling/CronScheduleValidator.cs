using Cronos;
using JobAutomation.Application.Interfaces;
using JobAutomation.Domain.Exceptions;

namespace JobAutomation.Infrastructure.Scheduling;

public class CronScheduleValidator : ICronScheduleValidator
{
    public void Validate(string? schedule)
    {
        if (string.IsNullOrWhiteSpace(schedule))
        {
            return;
        }

        var trimmed = schedule.Trim();

        try
        {
            CronExpression.Parse(trimmed, CronFormat.Standard);
        }
        catch (Exception)
        {
            throw new BadRequestException(
                "Invalid cron schedule.",
                new Dictionary<string, string[]>
                {
                    ["schedule"] =
                    [
                        "Invalid cron expression. Use 5-field UTC cron format: minute hour day-of-month month day-of-week."
                    ]
                });
        }
    }

    public DateTime? GetNextOccurrenceUtc(string? schedule, DateTime fromUtc)
    {
        if (string.IsNullOrWhiteSpace(schedule))
        {
            return null;
        }

        var expression = CronExpression.Parse(schedule.Trim(), CronFormat.Standard);
        return expression.GetNextOccurrence(fromUtc, TimeZoneInfo.Utc);
    }
}
