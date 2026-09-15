using JobAutomation.Application;

namespace JobAutomation.UnitTests.Scheduling;

public class ScheduledOccurrenceKeysTests
{
    [Fact]
    public void Build_SameJobAndOccurrence_ProducesDeterministicKey()
    {
        var jobId = Guid.Parse("8b6f1234-5678-90ab-cdef-1234567890ab");
        var occurrence = new DateTime(2026, 9, 14, 10, 0, 30, DateTimeKind.Utc);

        var key1 = ScheduledOccurrenceKeys.Build(jobId, occurrence);
        var key2 = ScheduledOccurrenceKeys.Build(jobId, occurrence);

        Assert.Equal(key1, key2);
        Assert.StartsWith("scheduled:8b6f1234-5678-90ab-cdef-1234567890ab:", key1);
        Assert.Contains("2026-09-14T10:00:00Z", key1);
    }

    [Fact]
    public void Build_DifferentOccurrences_ProduceDifferentKeys()
    {
        var jobId = Guid.NewGuid();
        var key1 = ScheduledOccurrenceKeys.Build(jobId, new DateTime(2026, 9, 14, 10, 0, 0, DateTimeKind.Utc));
        var key2 = ScheduledOccurrenceKeys.Build(jobId, new DateTime(2026, 9, 14, 10, 5, 0, DateTimeKind.Utc));

        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void Build_DifferentJobs_ProduceDifferentKeys()
    {
        var occurrence = new DateTime(2026, 9, 14, 10, 0, 0, DateTimeKind.Utc);
        var key1 = ScheduledOccurrenceKeys.Build(Guid.NewGuid(), occurrence);
        var key2 = ScheduledOccurrenceKeys.Build(Guid.NewGuid(), occurrence);

        Assert.NotEqual(key1, key2);
    }
}
