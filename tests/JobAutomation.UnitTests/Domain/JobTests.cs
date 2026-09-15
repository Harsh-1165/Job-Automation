using JobAutomation.Domain.Entities;
using JobAutomation.Domain.Enums;

namespace JobAutomation.UnitTests.Domain;

public class JobTests
{
    [Fact]
    public void NewJob_HasExpectedDefaults()
    {
        var now = DateTime.UtcNow;
        var userId = Guid.NewGuid();

        var job = new Job
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = "Daily sync",
            HttpMethod = "GET",
            TargetUrl = "https://api.example.com/health",
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        Assert.Equal(JobStatus.Draft, job.Status);
        Assert.Equal(3, job.MaxRetries);
        Assert.Equal(30, job.TimeoutSeconds);
        Assert.Empty(job.Executions);
    }

    [Fact]
    public void Execution_DefaultStatus_IsQueued()
    {
        var execution = new Execution
        {
            Id = Guid.NewGuid(),
            JobId = Guid.NewGuid(),
            IdempotencyKey = "manual:2025-09-14T12:00:00Z",
            TriggerType = TriggerType.Manual,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        Assert.Equal(ExecutionStatus.Queued, execution.Status);
        Assert.Equal(1, execution.AttemptNumber);
    }
}
