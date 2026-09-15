using JobAutomation.Domain.Enums;

namespace JobAutomation.Domain.Entities;

public class Execution
{
    public Guid Id { get; set; }

    public Guid JobId { get; set; }

    public ExecutionStatus Status { get; set; } = ExecutionStatus.Queued;

    public TriggerType TriggerType { get; set; }

    /// <summary>
    /// Client-provided idempotency key for Run Now duplicate protection.
    /// </summary>
    public required string IdempotencyKey { get; set; }

    /// <summary>
    /// Idempotency key from the last manual retry request, when applicable.
    /// </summary>
    public string? ManualRetryIdempotencyKey { get; set; }

    public int AttemptNumber { get; set; } = 1;

    public int? HttpStatusCode { get; set; }

    public string? ResponseBody { get; set; }

    public string? ErrorMessage { get; set; }

    public DateTime? StartedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public DateTime? NextRetryAtUtc { get; set; }

    /// <summary>
    /// Identifies the worker claim on this execution. Null when not Running.
    /// </summary>
    public Guid? LeaseId { get; set; }

    public DateTime? LeaseExpiresAtUtc { get; set; }

    public DateTime? LastHeartbeatAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    public Job Job { get; set; } = null!;

    public ICollection<ExecutionLog> Logs { get; set; } = new List<ExecutionLog>();
}
