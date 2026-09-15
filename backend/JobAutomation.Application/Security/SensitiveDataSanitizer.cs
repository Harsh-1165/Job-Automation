namespace JobAutomation.Application.Security;

public static class SensitiveDataSanitizer
{
    private static readonly HashSet<string> SensitiveHeaderNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Authorization",
            "Proxy-Authorization",
            "Cookie",
            "Set-Cookie",
            "X-Api-Key",
            "Api-Key",
            "X-Auth-Token",
            "X-API-Key"
        };

    private static readonly string[] SensitiveSubstrings =
    [
        "password",
        "secret",
        "token",
        "access_token",
        "refresh_token"
    ];

    public const string RedactedValue = "[REDACTED]";

    public static bool IsSensitiveHeader(string headerName)
    {
        if (SensitiveHeaderNames.Contains(headerName))
        {
            return true;
        }

        foreach (var fragment in SensitiveSubstrings)
        {
            if (headerName.Contains(fragment, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static Dictionary<string, string>? SanitizeHeaders(Dictionary<string, string>? headers)
    {
        if (headers is null || headers.Count == 0)
        {
            return headers;
        }

        return headers.ToDictionary(
            kvp => kvp.Key,
            kvp => IsSensitiveHeader(kvp.Key) ? RedactedValue : kvp.Value);
    }

    public static string SanitizeForLog(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        if (value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return "Bearer [REDACTED]";
        }

        return value;
    }
}
