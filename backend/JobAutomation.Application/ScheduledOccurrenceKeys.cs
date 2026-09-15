namespace JobAutomation.Application;

public static class ScheduledOccurrenceKeys
{
    public static string Build(Guid jobId, DateTime occurrenceUtc)
    {
        var normalized = TruncateToMinute(occurrenceUtc);
        return $"scheduled:{jobId:D}:{normalized:yyyy-MM-ddTHH:mm:00Z}";
    }

    public static DateTime TruncateToMinute(DateTime utc)
    {
        return new DateTime(
            utc.Year,
            utc.Month,
            utc.Day,
            utc.Hour,
            utc.Minute,
            0,
            DateTimeKind.Utc);
    }
}
