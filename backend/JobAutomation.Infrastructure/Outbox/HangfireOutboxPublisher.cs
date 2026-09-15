using System.Text.Json;
using JobAutomation.Application.Interfaces;
using JobAutomation.Application.Outbox;
using JobAutomation.Domain;
using JobAutomation.Domain.Entities;
using JobAutomation.Infrastructure.BackgroundJobs;
using Hangfire;

namespace JobAutomation.Infrastructure.Outbox;

public class HangfireOutboxPublisher : IOutboxPublisher
{
    public Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        switch (message.MessageType)
        {
            case OutboxMessageTypes.ExecutionEnqueue:
            {
                var payload = JsonSerializer.Deserialize<ExecutionEnqueuePayload>(message.Payload)
                    ?? throw new InvalidOperationException("Invalid ExecutionEnqueue payload.");

                BackgroundJob.Enqueue<ExecuteJobBackgroundTask>(
                    task => task.ExecuteAsync(payload.ExecutionId, CancellationToken.None));

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

                BackgroundJob.Schedule<ExecuteJobBackgroundTask>(
                    task => task.PrepareRetryAsync(payload.ExecutionId, CancellationToken.None),
                    delay);

                break;
            }
            default:
                throw new InvalidOperationException($"Unsupported outbox message type: {message.MessageType}");
        }

        return Task.CompletedTask;
    }
}
