using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace JobAutomation.Infrastructure.Authentication;

public static class JwtSettingsValidator
{
    private const int MinSecretLength = 32;

    public static JwtSettings Resolve(IConfiguration configuration, IHostEnvironment environment)
    {
        var settings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
            ?? new JwtSettings();

        var secret = configuration["JWT_SECRET"];
        var issuer = configuration["JWT_ISSUER"];
        var audience = configuration["JWT_AUDUENCE"];
        var expiration = configuration["JWT_EXPIRATION_MINUTES"];

        if (!string.IsNullOrWhiteSpace(secret))
        {
            settings.Secret = secret;
        }

        if (!string.IsNullOrWhiteSpace(issuer))
        {
            settings.Issuer = issuer;
        }

        if (!string.IsNullOrWhiteSpace(audience))
        {
            settings.Audience = audience;
        }

        if (int.TryParse(expiration, out var expirationMinutes) && expirationMinutes > 0)
        {
            settings.ExpirationMinutes = expirationMinutes;
        }

        if (string.IsNullOrWhiteSpace(settings.Secret))
        {
            if (environment.IsProduction())
            {
                throw new InvalidOperationException(
                    "JWT_SECRET must be configured in production.");
            }

            settings.Secret = "development-only-placeholder-secret-min-32-chars!!";
        }
        else if (settings.Secret.Length < MinSecretLength)
        {
            throw new InvalidOperationException(
                $"JWT secret must be at least {MinSecretLength} characters.");
        }

        if (string.IsNullOrWhiteSpace(settings.Issuer))
        {
            settings.Issuer = "JobAutomation";
        }

        if (string.IsNullOrWhiteSpace(settings.Audience))
        {
            settings.Audience = "JobAutomation";
        }

        return settings;
    }
}
