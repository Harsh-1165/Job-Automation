using JobAutomation.Domain;
using JobAutomation.Domain.Entities;
using JobAutomation.Domain.Enums;
using JobAutomation.Domain.Exceptions;

namespace JobAutomation.UnitTests.Domain;

public class JobStatusTransitionTests
{
    private static Job CreateJob(JobStatus status) => new()
    {
        Id = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        Name = "Test",
        HttpMethod = "GET",
        TargetUrl = "https://example.com",
        Status = status,
        CreatedAtUtc = DateTime.UtcNow,
        UpdatedAtUtc = DateTime.UtcNow
    };

    [Fact]
    public void Enable_PausedJob_BecomesActive()
    {
        var job = CreateJob(JobStatus.Paused);
        JobStatusTransitions.Enable(job);
        Assert.Equal(JobStatus.Active, job.Status);
    }

    [Fact]
    public void Enable_ActiveJob_Throws()
    {
        var job = CreateJob(JobStatus.Active);
        Assert.Throws<DomainException>(() => JobStatusTransitions.Enable(job));
    }

    [Fact]
    public void Disable_ActiveJob_BecomesPaused()
    {
        var job = CreateJob(JobStatus.Active);
        JobStatusTransitions.Disable(job);
        Assert.Equal(JobStatus.Paused, job.Status);
    }

    [Fact]
    public void Disable_ArchivedJob_Throws()
    {
        var job = CreateJob(JobStatus.Archived);
        Assert.Throws<DomainException>(() => JobStatusTransitions.Disable(job));
    }

    [Fact]
    public void Archive_ActiveJob_BecomesArchived()
    {
        var job = CreateJob(JobStatus.Active);
        JobStatusTransitions.Archive(job);
        Assert.Equal(JobStatus.Archived, job.Status);
    }

    [Fact]
    public void Archive_AlreadyArchived_Throws()
    {
        var job = CreateJob(JobStatus.Archived);
        Assert.Throws<DomainException>(() => JobStatusTransitions.Archive(job));
    }

    [Fact]
    public void EnsureEditable_ArchivedJob_Throws()
    {
        var job = CreateJob(JobStatus.Archived);
        Assert.Throws<DomainException>(() => JobStatusTransitions.EnsureEditable(job));
    }
}
