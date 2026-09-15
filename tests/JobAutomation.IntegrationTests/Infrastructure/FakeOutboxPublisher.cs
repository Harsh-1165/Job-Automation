using System.Text.Json;
using JobAutomation.Application.Interfaces;
using JobAutomation.Application.Outbox;
using JobAutomation.Domain;
using JobAutomation.Domain.Entities;

namespace JobAutomation.IntegrationTests.Infrastructure;

public class FakeOutboxPublisher : IOutboxPublisher
{
    private readonly FakeBackgroundJobEnqueuer _enqueuer;

    public FakeOutboxPublisher(FakeBackgroundJobEnqueuer enqueuer)
    {
        _enqueuer = enqueuer;
    }

    public Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        switch (message.MessageType)
        {
            case OutboxMessageTypes.ExecutionEnqueue:
            {
                var payload = JsonSerializer.Deserialize<ExecutionEnqueuePayload>(message.Payload)
                    ?? throw new InvalidOperationException("Invalid ExecutionEnqueue payload.");
                _enqueuer.EnqueueExecution(payload.ExecutionId);
                break;
            }
            case OutboxMessageTypes.RetryPreparation:
            {
                var payload = JsonSerializer.Deserialize<RetryPreparationPayload>(message.Payload)
                    ?? throw new InvalidOperationException("Invalid RetryPreparation payload.");
                var delay = payload.ScheduledForUtc - DateTime.UtcNow;
                if (delay < TimeSpan.Zero)
                {
                    delay = TimeSpan.Zero;
                }

                _enqueuer.ScheduleRetryPreparation(payload.ExecutionId, delay);
                break;
            }
            default:
                throw new InvalidOperationException($"Unsupported message type: {message.MessageType}");
        }

        return Task.CompletedTask;
    }
}
