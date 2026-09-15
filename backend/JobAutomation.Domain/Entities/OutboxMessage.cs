using JobAutomation.Domain.Enums;

namespace JobAutomation.Domain.Entities;

public class OutboxMessage
{
    public Guid Id { get; set; }

    public required string MessageType { get; set; }

    public required string Payload { get; set; }

    /// <summary>
    /// Denormalized for dispatcher/recovery queries.
    /// </summary>
    public Guid? ExecutionId { get; set; }

    public OutboxMessageStatus Status { get; set; } = OutboxMessageStatus.Pending;

    public int AttemptCount { get; set; }

    public DateTime? NextAttemptAtUtc { get; set; }

    public string? LastError { get; set; }

    public DateTime? LockedUntilUtc { get; set; }

    public Guid? LockId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? ProcessedAtUtc { get; set; }
}
