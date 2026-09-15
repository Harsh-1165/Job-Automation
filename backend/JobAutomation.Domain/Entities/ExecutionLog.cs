namespace JobAutomation.Domain.Entities;

public class ExecutionLog
{
    public Guid Id { get; set; }

    public Guid ExecutionId { get; set; }

    public required string Level { get; set; }

    public required string Message { get; set; }

    public string? Details { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public Execution Execution { get; set; } = null!;
}
