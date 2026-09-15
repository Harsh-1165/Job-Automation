using JobAutomation.Application.Interfaces;
using JobAutomation.Infrastructure.Services;
using JobAutomation.Infrastructure.Worker;
using Microsoft.Extensions.DependencyInjection;

namespace JobAutomation.Infrastructure.Extensions;

public static class WorkerInfrastructureExtensions
{
    public static IServiceCollection AddWorkerInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<ICurrentUserService, WorkerCurrentUserService>();
        services.AddSingleton<WorkerIdentity>();
        services.AddScoped<IWorkerHeartbeatService, WorkerHeartbeatService>();
        services.AddHostedService<WorkerHeartbeatHostedService>();

        return services;
    }
}
