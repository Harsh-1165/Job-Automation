using JobAutomation.Domain;
using JobAutomation.Domain.Entities;
using JobAutomation.Domain.Enums;
using JobAutomation.Domain.Exceptions;

namespace JobAutomation.UnitTests.Domain;

public class ExecutionStatusTransitionTests
{
    private static Execution CreateExecution(ExecutionStatus status) => new()
    {
        Id = Guid.NewGuid(),
        JobId = Guid.NewGuid(),
        Status = status,
        TriggerType = TriggerType.Manual,
        IdempotencyKey = "manual:test",
        CreatedAtUtc = DateTime.UtcNow,
        UpdatedAtUtc = DateTime.UtcNow
    };

    [Fact]
    public void MarkRunning_FromQueued_Succeeds()
    {
        var execution = CreateExecution(ExecutionStatus.Queued);
        ExecutionStatusTransitions.MarkRunning(execution);
        Assert.Equal(ExecutionStatus.Running, execution.Status);
        Assert.NotNull(execution.StartedAtUtc);
    }

    [Fact]
    public void MarkRunning_FromRunning_Throws()
    {
        var execution = CreateExecution(ExecutionStatus.Running);
        Assert.Throws<DomainException>(() => ExecutionStatusTransitions.MarkRunning(execution));
    }

    [Fact]
    public void MarkSucceeded_FromRunning_Succeeds()
    {
        var execution = CreateExecution(ExecutionStatus.Running);
        execution.StartedAtUtc = DateTime.UtcNow.AddSeconds(-1);
        ExecutionStatusTransitions.MarkSucceeded(execution);
        Assert.Equal(ExecutionStatus.Succeeded, execution.Status);
        Assert.NotNull(execution.CompletedAtUtc);
    }

    [Fact]
    public void MarkFailed_FromRunning_Succeeds()
    {
        var execution = CreateExecution(ExecutionStatus.Running);
        ExecutionStatusTransitions.MarkFailed(execution, "timeout");
        Assert.Equal(ExecutionStatus.Failed, execution.Status);
        Assert.Equal("timeout", execution.ErrorMessage);
    }

    [Fact]
    public void CanProcess_OnlyQueued_ReturnsTrue()
    {
        Assert.True(ExecutionStatusTransitions.CanProcess(CreateExecution(ExecutionStatus.Queued)));
        Assert.False(ExecutionStatusTransitions.CanProcess(CreateExecution(ExecutionStatus.Running)));
    }

    [Fact]
    public void MarkRetrying_FromRunning_Succeeds()
    {
        var execution = CreateExecution(ExecutionStatus.Running);
        var nextRetry = DateTime.UtcNow.AddSeconds(10);
        ExecutionStatusTransitions.MarkRetrying(execution, nextRetry, "HTTP 503");

        Assert.Equal(ExecutionStatus.Retrying, execution.Status);
        Assert.Equal(nextRetry, execution.NextRetryAtUtc);
        Assert.Null(execution.LeaseId);
    }

    [Fact]
    public void MarkRetryQueued_FromRetrying_IncrementsAttempt()
    {
        var execution = CreateExecution(ExecutionStatus.Retrying);
        execution.AttemptNumber = 1;
        ExecutionStatusTransitions.MarkRetryQueued(execution);

        Assert.Equal(ExecutionStatus.Queued, execution.Status);
        Assert.Equal(2, execution.AttemptNumber);
        Assert.Null(execution.NextRetryAtUtc);
    }

    [Fact]
    public void MarkRetrying_FromSucceeded_Throws()
    {
        var execution = CreateExecution(ExecutionStatus.Succeeded);
        Assert.Throws<DomainException>(() =>
            ExecutionStatusTransitions.MarkRetrying(execution, DateTime.UtcNow.AddSeconds(5)));
    }

    [Fact]
    public void MarkRecoveredFromStale_FromRunning_ClearsLeaseAndRequeues()
    {
        var execution = CreateExecution(ExecutionStatus.Running);
        execution.LeaseId = Guid.NewGuid();
        execution.LeaseExpiresAtUtc = DateTime.UtcNow.AddMinutes(-1);
        execution.LastHeartbeatAtUtc = DateTime.UtcNow.AddMinutes(-2);

        ExecutionStatusTransitions.MarkRecoveredFromStale(execution);

        Assert.Equal(ExecutionStatus.Queued, execution.Status);
        Assert.Null(execution.LeaseId);
        Assert.Null(execution.LeaseExpiresAtUtc);
        Assert.Null(execution.LastHeartbeatAtUtc);
    }

    [Fact]
    public void MarkCancelled_FromQueued_Succeeds()
    {
        var execution = CreateExecution(ExecutionStatus.Queued);
        ExecutionStatusTransitions.MarkCancelled(execution);

        Assert.Equal(ExecutionStatus.Cancelled, execution.Status);
        Assert.NotNull(execution.CompletedAtUtc);
    }

    [Fact]
    public void MarkCancelled_FromRetrying_Succeeds()
    {
        var execution = CreateExecution(ExecutionStatus.Retrying);
        execution.NextRetryAtUtc = DateTime.UtcNow.AddMinutes(1);
        ExecutionStatusTransitions.MarkCancelled(execution);

        Assert.Equal(ExecutionStatus.Cancelled, execution.Status);
        Assert.Null(execution.NextRetryAtUtc);
    }

    [Fact]
    public void MarkCancelled_FromRunning_Throws()
    {
        var execution = CreateExecution(ExecutionStatus.Running);
        Assert.Throws<DomainException>(() => ExecutionStatusTransitions.MarkCancelled(execution));
    }

    [Fact]
    public void MarkManualRetryQueued_FromFailed_ResetsAttempt()
    {
        var execution = CreateExecution(ExecutionStatus.Failed);
        execution.AttemptNumber = 3;
        execution.CompletedAtUtc = DateTime.UtcNow;
        execution.ErrorMessage = "error";

        ExecutionStatusTransitions.MarkManualRetryQueued(execution, Guid.NewGuid().ToString());

        Assert.Equal(ExecutionStatus.Queued, execution.Status);
        Assert.Equal(TriggerType.ManualRetry, execution.TriggerType);
        Assert.Equal(1, execution.AttemptNumber);
        Assert.Null(execution.CompletedAtUtc);
        Assert.Null(execution.ErrorMessage);
    }

    [Fact]
    public void MarkRunning_FromCancelled_Throws()
    {
        var execution = CreateExecution(ExecutionStatus.Cancelled);
        Assert.Throws<DomainException>(() => ExecutionStatusTransitions.MarkRunning(execution));
    }

    [Fact]
    public void MarkRetryQueued_FromCancelled_Throws()
    {
        var execution = CreateExecution(ExecutionStatus.Cancelled);
        Assert.Throws<DomainException>(() => ExecutionStatusTransitions.MarkRetryQueued(execution));
    }

    [Fact]
    public void MarkManualRetryQueued_FromCancelled_Throws()
    {
        var execution = CreateExecution(ExecutionStatus.Cancelled);
        Assert.Throws<DomainException>(() =>
            ExecutionStatusTransitions.MarkManualRetryQueued(execution, Guid.NewGuid().ToString()));
    }

    [Fact]
    public void MarkSucceeded_FromCancelled_Throws()
    {
        var execution = CreateExecution(ExecutionStatus.Cancelled);
        Assert.Throws<DomainException>(() => ExecutionStatusTransitions.MarkSucceeded(execution));
    }
}
