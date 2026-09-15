using JobAutomation.Domain.Enums;

namespace JobAutomation.Domain.Entities;

public class Job
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    public JobStatus Status { get; set; } = JobStatus.Draft;

    public required string HttpMethod { get; set; }

    public required string TargetUrl { get; set; }

    public string? RequestHeadersJson { get; set; }

    public string? RequestBody { get; set; }

    public string? CronExpression { get; set; }

    public DateTime? LastRunAtUtc { get; set; }

    public DateTime? NextRunAtUtc { get; set; }

    public int MaxRetries { get; set; } = 3;

    public int TimeoutSeconds { get; set; } = 30;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public User User { get; set; } = null!;

    public ICollection<Execution> Executions { get; set; } = new List<Execution>();
}
