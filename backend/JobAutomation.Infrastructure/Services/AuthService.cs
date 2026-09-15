using JobAutomation.Application.DTOs.Auth;
using JobAutomation.Application.Helpers;
using JobAutomation.Application.Interfaces;
using JobAutomation.Application.Validation;
using JobAutomation.Domain.Entities;
using JobAutomation.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace JobAutomation.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IApplicationDbContext dbContext,
        ICurrentUserService currentUser,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        ILogger<AuthService> logger)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<AuthResponse> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        AuthRequestValidator.ValidateRegister(request);

        var normalizedEmail = EmailNormalizer.Normalize(request.Email);
        var now = DateTime.UtcNow;

        var existing = await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

        if (existing)
        {
            _logger.LogWarning("Registration failed: duplicate email for {NormalizedEmail}", normalizedEmail);
            throw new ConflictException("An account with this email already exists.");
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email.Trim(),
            NormalizedEmail = normalizedEmail,
            PasswordHash = _passwordHasher.HashPassword(request.Password),
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        _dbContext.Users.Add(user);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            _logger.LogWarning(ex, "Registration failed due to duplicate email constraint for {NormalizedEmail}", normalizedEmail);
            throw new ConflictException("An account with this email already exists.");
        }

        _logger.LogInformation("User registered successfully with id {UserId}", user.Id);

        return _tokenService.GenerateAuthResponse(user);
    }

    public async Task<AuthResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        AuthRequestValidator.ValidateLogin(request);

        var normalizedEmail = EmailNormalizer.Normalize(request.Email);

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

        if (user is null || !_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            _logger.LogWarning("Login failed for {NormalizedEmail}", normalizedEmail);
            throw new UnauthorizedException();
        }

        _logger.LogInformation("User logged in successfully with id {UserId}", user.Id);

        return _tokenService.GenerateAuthResponse(user);
    }

    public async Task<UserProfileResponse> GetProfileAsync(CancellationToken cancellationToken = default)
    {
        var userId = _currentUser.GetRequiredUserId();

        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            throw new NotFoundException("User not found.");
        }

        return new UserProfileResponse
        {
            UserId = user.Id,
            Email = user.Email,
            IsAdmin = user.IsAdmin
        };
    }

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
