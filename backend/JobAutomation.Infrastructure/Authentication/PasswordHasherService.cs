using JobAutomation.Application.Interfaces;
using JobAutomation.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace JobAutomation.Infrastructure.Authentication;

public class PasswordHasherService : IPasswordHasher
{
    private readonly PasswordHasher<User> _hasher = new();

    public string HashPassword(string password) =>
        _hasher.HashPassword(null!, password);

    public bool VerifyPassword(string password, string passwordHash) =>
        _hasher.VerifyHashedPassword(null!, passwordHash, password)
            is not PasswordVerificationResult.Failed;
}
