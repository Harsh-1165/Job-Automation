using JobAutomation.Domain.Entities;

namespace JobAutomation.Application.Interfaces;

/// <summary>
/// User management abstraction. Full registration/login will be implemented in a later phase.
/// </summary>
public interface IUserService
{
    Task<User?> GetByIdAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
}
