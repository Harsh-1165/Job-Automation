namespace JobAutomation.Application;

public enum DashboardTimeRange
{
    Hours24 = 0,
    Days7 = 1,
    Days30 = 2
}

public static class DashboardTimeRangeParser
{
    public static DashboardTimeRange Parse(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "24h" or "24hours" => DashboardTimeRange.Hours24,
            "7d" or "7days" => DashboardTimeRange.Days7,
            "30d" or "30days" => DashboardTimeRange.Days30,
            null or "" => DashboardTimeRange.Hours24,
            _ => throw new ArgumentException($"Invalid dashboard range: {value}. Use 24h, 7d, or 30d.")
        };

    public static DateTime GetStartUtc(DashboardTimeRange range, DateTime nowUtc) =>
        range switch
        {
            DashboardTimeRange.Hours24 => nowUtc.AddHours(-24),
            DashboardTimeRange.Days7 => nowUtc.AddDays(-7),
            DashboardTimeRange.Days30 => nowUtc.AddDays(-30),
            _ => nowUtc.AddHours(-24)
        };
}
