using JobAutomation.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace JobAutomation.Application.Interfaces;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }

    DbSet<Job> Jobs { get; }

    DbSet<Execution> Executions { get; }

    DbSet<ExecutionLog> ExecutionLogs { get; }

    DbSet<OutboxMessage> OutboxMessages { get; }

    DbSet<WorkerHeartbeat> WorkerHeartbeats { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
