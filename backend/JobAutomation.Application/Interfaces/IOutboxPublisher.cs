using JobAutomation.Domain.Entities;

namespace JobAutomation.Application.Interfaces;

public interface IOutboxPublisher
{
    Task PublishAsync(OutboxMessage message, CancellationToken cancellationToken = default);
}
