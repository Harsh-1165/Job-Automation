using JobAutomation.Domain.Entities;
using JobAutomation.Domain.Enums;
using JobAutomation.Domain.Exceptions;

namespace JobAutomation.Domain;

public static class ExecutionStatusTransitions
{
    public static void MarkRunning(Execution execution)
    {
        if (execution.Status != ExecutionStatus.Queued)
        {
            throw new DomainException(
                "INVALID_EXECUTION_STATE",
                $"Execution cannot start from status {execution.Status}.");
        }

        execution.Status = ExecutionStatus.Running;
        execution.StartedAtUtc ??= DateTime.UtcNow;
    }

    public static void MarkSucceeded(Execution execution)
    {
        if (execution.Status != ExecutionStatus.Running)
        {
            throw new DomainException(
                "INVALID_EXECUTION_STATE",
                $"Execution cannot succeed from status {execution.Status}.");
        }

        execution.Status = ExecutionStatus.Succeeded;
        execution.CompletedAtUtc = DateTime.UtcNow;
    }

    public static void MarkFailed(Execution execution, string? errorMessage = null)
    {
        if (execution.Status is ExecutionStatus.Succeeded or ExecutionStatus.Cancelled)
        {
            throw new DomainException(
                "INVALID_EXECUTION_STATE",
                $"Execution cannot fail from status {execution.Status}.");
        }

        execution.Status = ExecutionStatus.Failed;
        execution.CompletedAtUtc = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(errorMessage))
        {
            execution.ErrorMessage = errorMessage;
        }
    }

    public static void MarkRetrying(Execution execution, DateTime nextRetryAtUtc, string? errorMessage = null)
    {
        if (execution.Status != ExecutionStatus.Running)
        {
            throw new DomainException(
                "INVALID_EXECUTION_STATE",
                $"Execution cannot enter retrying from status {execution.Status}.");
        }

        execution.Status = ExecutionStatus.Retrying;
        execution.NextRetryAtUtc = nextRetryAtUtc;
        execution.LeaseId = null;
        execution.LeaseExpiresAtUtc = null;
        execution.LastHeartbeatAtUtc = null;

        if (!string.IsNullOrWhiteSpace(errorMessage))
        {
            execution.ErrorMessage = errorMessage;
        }
    }

    public static void MarkRetryQueued(Execution execution)
    {
        if (execution.Status != ExecutionStatus.Retrying)
        {
            throw new DomainException(
                "INVALID_EXECUTION_STATE",
                $"Execution cannot queue retry from status {execution.Status}.");
        }

        execution.Status = ExecutionStatus.Queued;
        execution.AttemptNumber += 1;
        execution.NextRetryAtUtc = null;
    }

    public static void MarkRecoveredFromStale(Execution execution)
    {
        if (execution.Status != ExecutionStatus.Running)
        {
            throw new DomainException(
                "INVALID_EXECUTION_STATE",
                $"Execution cannot be recovered from status {execution.Status}.");
        }

        execution.Status = ExecutionStatus.Queued;
        execution.LeaseId = null;
        execution.LeaseExpiresAtUtc = null;
        execution.LastHeartbeatAtUtc = null;
    }

    public static void MarkCancelled(Execution execution)
    {
        if (execution.Status is not (ExecutionStatus.Queued or ExecutionStatus.Retrying))
        {
            throw new DomainException(
                "INVALID_EXECUTION_STATE",
                $"Execution cannot be cancelled from status {execution.Status}.");
        }

        execution.Status = ExecutionStatus.Cancelled;
        execution.CompletedAtUtc = DateTime.UtcNow;
        execution.LeaseId = null;
        execution.LeaseExpiresAtUtc = null;
        execution.LastHeartbeatAtUtc = null;
        execution.NextRetryAtUtc = null;
    }

    public static void MarkManualRetryQueued(Execution execution, string idempotencyKey)
    {
        if (execution.Status != ExecutionStatus.Failed)
        {
            throw new DomainException(
                "INVALID_EXECUTION_STATE",
                $"Manual retry requires Failed status, not {execution.Status}.");
        }

        execution.Status = ExecutionStatus.Queued;
        execution.TriggerType = TriggerType.ManualRetry;
        execution.AttemptNumber = 1;
        execution.ManualRetryIdempotencyKey = idempotencyKey;
        execution.StartedAtUtc = null;
        execution.CompletedAtUtc = null;
        execution.HttpStatusCode = null;
        execution.ResponseBody = null;
        execution.ErrorMessage = null;
        execution.NextRetryAtUtc = null;
        execution.LeaseId = null;
        execution.LeaseExpiresAtUtc = null;
        execution.LastHeartbeatAtUtc = null;
    }

    public static bool CanProcess(Execution execution) =>
        execution.Status == ExecutionStatus.Queued;
}
