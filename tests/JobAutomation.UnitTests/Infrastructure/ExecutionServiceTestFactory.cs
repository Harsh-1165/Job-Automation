using JobAutomation.Application;
using JobAutomation.Application.Interfaces;
using JobAutomation.Domain;
using JobAutomation.Domain.Entities;
using JobAutomation.Infrastructure.Outbox;
using JobAutomation.Infrastructure.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace JobAutomation.UnitTests.Infrastructure;

public static class ExecutionServiceTestFactory
{
    public static ExecutionService Create(
        IApplicationDbContext db,
        ICurrentUserService currentUser,
        IHttpJobExecutor executor,
        IExecutionConcurrencyService? concurrency = null,
        IRetryPolicy? retryPolicy = null,
        ISystemClock? clock = null,
        ExecutionLeaseOptions? leaseOptions = null,
        IOutboxWriter? outboxWriter = null)
    {
        var options = Options.Create(leaseOptions ?? new ExecutionLeaseOptions());
        return new ExecutionService(
            db,
            currentUser,
            executor,
            outboxWriter ?? new OutboxWriter(db),
            concurrency ?? new ExecutionConcurrencyService(
                db,
                options,
                NullLogger<ExecutionConcurrencyService>.Instance),
            retryPolicy ?? new RetryPolicy(Options.Create(new RetryOptions { JitterPercentage = 0 })),
            clock ?? new FixedClock(DateTime.UtcNow),
            options,
            NullLogger<ExecutionService>.Instance);
    }
}

public sealed class FixedClock(DateTime utcNow) : ISystemClock
{
    public DateTime UtcNow { get; set; } = utcNow;
}

public sealed class TrackingOutboxPublisher : IOutboxPublisher
{
    public List<Guid> EnqueuedExecutionIds { get; } = [];

    public List<(Guid ExecutionId, DateTime ScheduledForUtc)> ScheduledRetries { get; } = [];

    public Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken = default)
    {
        if (message.MessageType == OutboxMessageTypes.ExecutionEnqueue)
        {
            var payload = System.Text.Json.JsonSerializer.Deserialize<Application.Outbox.ExecutionEnqueuePayload>(message.Payload)
                ?? throw new InvalidOperationException("Invalid payload");
            EnqueuedExecutionIds.Add(payload.ExecutionId);
        }
        else if (message.MessageType == OutboxMessageTypes.RetryPreparation)
        {
            var payload = System.Text.Json.JsonSerializer.Deserialize<Application.Outbox.RetryPreparationPayload>(message.Payload)
                ?? throw new InvalidOperationException("Invalid payload");
            ScheduledRetries.Add((payload.ExecutionId, payload.ScheduledForUtc));
        }

        return Task.CompletedTask;
    }
}
