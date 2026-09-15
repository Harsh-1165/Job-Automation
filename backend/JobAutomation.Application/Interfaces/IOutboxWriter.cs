namespace JobAutomation.Application.Interfaces;

public interface IOutboxWriter
{
    void AddExecutionEnqueue(Guid executionId, DateTime createdAtUtc);

    void AddRetryPreparation(Guid executionId, DateTime scheduledForUtc, DateTime createdAtUtc);
}
