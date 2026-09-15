using JobAutomation.Domain.Entities;
using JobAutomation.Domain.Enums;
using JobAutomation.Domain.Exceptions;

namespace JobAutomation.Domain;

public static class JobStatusTransitions
{
    public static void Enable(Job job)
    {
        if (job.Status == JobStatus.Archived)
        {
            throw new DomainException("INVALID_STATUS_TRANSITION", "Archived jobs cannot be enabled.");
        }

        if (job.Status != JobStatus.Paused)
        {
            throw new DomainException("INVALID_STATUS_TRANSITION", "Only paused jobs can be enabled.");
        }

        job.Status = JobStatus.Active;
    }

    public static void Disable(Job job)
    {
        if (job.Status == JobStatus.Archived)
        {
            throw new DomainException("INVALID_STATUS_TRANSITION", "Archived jobs cannot be disabled.");
        }

        if (job.Status != JobStatus.Active)
        {
            throw new DomainException("INVALID_STATUS_TRANSITION", "Only active jobs can be disabled.");
        }

        job.Status = JobStatus.Paused;
    }

    public static void Archive(Job job)
    {
        if (job.Status == JobStatus.Archived)
        {
            throw new DomainException("ALREADY_ARCHIVED", "Job is already archived.");
        }

        job.Status = JobStatus.Archived;
    }

    public static void EnsureEditable(Job job)
    {
        if (job.Status == JobStatus.Archived)
        {
            throw new DomainException("JOB_ARCHIVED", "Archived jobs cannot be modified.");
        }
    }
}
