namespace JobAutomation.Application;

public static class RecurringJobIds
{
    public static string ForJob(Guid jobId) => $"job:{jobId:D}:recurring";
}
