using System.Text.Json;
using JobAutomation.Application.Interfaces;
using JobAutomation.Application.Outbox;
using JobAutomation.Domain;
using JobAutomation.Domain.Entities;
using JobAutomation.Domain.Enums;

namespace JobAutomation.Infrastructure.Outbox;

public class OutboxWriter : IOutboxWriter
{
    private readonly IApplicationDbContext _dbContext;

    public OutboxWriter(IApplicationDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public void AddExecutionEnqueue(Guid executionId, DateTime createdAtUtc)
    {
        var payload = JsonSerializer.Serialize(new ExecutionEnqueuePayload
        {
            ExecutionId = executionId
        });

        _dbContext.OutboxMessages.Add(CreateMessage(
            OutboxMessageTypes.ExecutionEnqueue,
            payload,
            executionId,
            createdAtUtc));
    }

    public void AddRetryPreparation(Guid executionId, DateTime scheduledForUtc, DateTime createdAtUtc)
    {
        var payload = JsonSerializer.Serialize(new RetryPreparationPayload
        {
            ExecutionId = executionId,
            ScheduledForUtc = scheduledForUtc
        });

        _dbContext.OutboxMessages.Add(CreateMessage(
            OutboxMessageTypes.RetryPreparation,
            payload,
            executionId,
            createdAtUtc));
    }

    private static OutboxMessage CreateMessage(
        string messageType,
        string payload,
        Guid executionId,
        DateTime createdAtUtc) =>
        new()
        {
            Id = Guid.NewGuid(),
            MessageType = messageType,
            Payload = payload,
            ExecutionId = executionId,
            Status = OutboxMessageStatus.Pending,
            AttemptCount = 0,
            CreatedAtUtc = createdAtUtc
        };
}
