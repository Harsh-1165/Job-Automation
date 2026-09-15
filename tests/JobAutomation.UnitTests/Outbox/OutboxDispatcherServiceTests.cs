using JobAutomation.Application;
using JobAutomation.Domain;
using JobAutomation.Domain.Entities;
using JobAutomation.Domain.Enums;
using JobAutomation.Infrastructure.Outbox;
using JobAutomation.Infrastructure.Persistence;
using JobAutomation.UnitTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace JobAutomation.UnitTests.Outbox;

public class OutboxDispatcherServiceTests
{
    [Fact]
    public async Task DispatchBatch_PublishesExecutionEnqueueAndMarksProcessed()
    {
        var (db, connection) = await SqliteTestDb.CreateAsync();
        await using (connection)
        await using (db)
        {
            var executionId = Guid.NewGuid();
            var publisher = new TrackingOutboxPublisher();
            var clock = new FixedClock(DateTime.UtcNow);
            var dispatcher = CreateDispatcher(db, publisher, clock);

            db.OutboxMessages.Add(new OutboxMessage
            {
                Id = Guid.NewGuid(),
                MessageType = OutboxMessageTypes.ExecutionEnqueue,
                Payload = $"{{\"ExecutionId\":\"{executionId}\"}}",
                ExecutionId = executionId,
                Status = OutboxMessageStatus.Pending,
                CreatedAtUtc = clock.UtcNow
            });
            await SqliteTestDb.SaveAndClearAsync(db);

            var processed = await dispatcher.DispatchBatchAsync();
            Assert.Equal(1, processed);
            Assert.Contains(executionId, publisher.EnqueuedExecutionIds);

            var message = await db.OutboxMessages.AsNoTracking().FirstAsync();
            Assert.Equal(OutboxMessageStatus.Processed, message.Status);
            Assert.NotNull(message.ProcessedAtUtc);
        }
    }

    [Fact]
    public async Task DispatchBatch_PublishFailure_IncrementsAttemptAndSchedulesRetry()
    {
        var (db, connection) = await SqliteTestDb.CreateAsync();
        await using (connection)
        await using (db)
        {
            var messageId = Guid.NewGuid();
            var clock = new FixedClock(DateTime.UtcNow);
            var dispatcher = CreateDispatcher(db, new FailingOutboxPublisher(), clock);

            db.OutboxMessages.Add(new OutboxMessage
            {
                Id = messageId,
                MessageType = OutboxMessageTypes.ExecutionEnqueue,
                Payload = $"{{\"ExecutionId\":\"{Guid.NewGuid()}\"}}",
                Status = OutboxMessageStatus.Pending,
                CreatedAtUtc = clock.UtcNow
            });
            await SqliteTestDb.SaveAndClearAsync(db);

            await dispatcher.DispatchBatchAsync();

            var message = await db.OutboxMessages.AsNoTracking().FirstAsync(m => m.Id == messageId);
            Assert.Equal(OutboxMessageStatus.Pending, message.Status);
            Assert.Equal(1, message.AttemptCount);
            Assert.NotNull(message.LastError);
            Assert.NotNull(message.NextAttemptAtUtc);
        }
    }

    [Fact]
    public async Task DispatchBatch_RespectsNextAttemptAtUtc()
    {
        var (db, connection) = await SqliteTestDb.CreateAsync();
        await using (connection)
        await using (db)
        {
            var clock = new FixedClock(DateTime.UtcNow);
            var publisher = new TrackingOutboxPublisher();
            var dispatcher = CreateDispatcher(db, publisher, clock);

            db.OutboxMessages.Add(new OutboxMessage
            {
                Id = Guid.NewGuid(),
                MessageType = OutboxMessageTypes.ExecutionEnqueue,
                Payload = $"{{\"ExecutionId\":\"{Guid.NewGuid()}\"}}",
                Status = OutboxMessageStatus.Pending,
                NextAttemptAtUtc = clock.UtcNow.AddMinutes(5),
                CreatedAtUtc = clock.UtcNow
            });
            await SqliteTestDb.SaveAndClearAsync(db);

            var processed = await dispatcher.DispatchBatchAsync();
            Assert.Equal(0, processed);
            Assert.Empty(publisher.EnqueuedExecutionIds);
        }
    }

    [Fact]
    public async Task DispatchBatch_ExpiredLeaseCanBeReclaimed()
    {
        var (db, connection) = await SqliteTestDb.CreateAsync();
        await using (connection)
        await using (db)
        {
            var executionId = Guid.NewGuid();
            var clock = new FixedClock(DateTime.UtcNow);
            var publisher = new TrackingOutboxPublisher();
            var dispatcher = CreateDispatcher(db, publisher, clock);

            db.OutboxMessages.Add(new OutboxMessage
            {
                Id = Guid.NewGuid(),
                MessageType = OutboxMessageTypes.ExecutionEnqueue,
                Payload = $"{{\"ExecutionId\":\"{executionId}\"}}",
                ExecutionId = executionId,
                Status = OutboxMessageStatus.Processing,
                LockedUntilUtc = clock.UtcNow.AddMinutes(-1),
                LockId = Guid.NewGuid(),
                CreatedAtUtc = clock.UtcNow
            });
            await SqliteTestDb.SaveAndClearAsync(db);

            var processed = await dispatcher.DispatchBatchAsync();
            Assert.Equal(1, processed);

            var message = await db.OutboxMessages.AsNoTracking().FirstAsync();
            Assert.Equal(OutboxMessageStatus.Processed, message.Status);
        }
    }

    private static OutboxDispatcherService CreateDispatcher(
        ApplicationDbContext db,
        Application.Interfaces.IOutboxPublisher publisher,
        FixedClock clock) =>
        new(
            db,
            publisher,
            Options.Create(new OutboxOptions
            {
                BatchSize = 100,
                LeaseDurationSeconds = 60,
                MaxPublishAttempts = 10,
                BaseRetryDelaySeconds = 5,
                MaxRetryDelaySeconds = 300
            }),
            clock,
            NullLogger<OutboxDispatcherService>.Instance);

    private sealed class FailingOutboxPublisher : Application.Interfaces.IOutboxPublisher
    {
        public Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Simulated Hangfire failure.");
    }
}
