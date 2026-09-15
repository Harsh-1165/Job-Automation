using JobAutomation.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace JobAutomation.IntegrationTests.Infrastructure;

public static class OutboxTestHelper
{
    public static async Task DispatchAllAsync(ApiWebApplicationFactory factory, CancellationToken cancellationToken = default)
    {
        using var scope = factory.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IOutboxDispatcher>();

        while (await dispatcher.DispatchBatchAsync(cancellationToken) > 0)
        {
            // Drain pending outbox messages.
        }
    }
}
