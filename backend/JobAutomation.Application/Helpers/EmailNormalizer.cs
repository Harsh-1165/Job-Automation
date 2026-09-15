namespace JobAutomation.Application.Helpers;

public static class EmailNormalizer
{
    public static string Normalize(string email) =>
        email.Trim().ToLowerInvariant();
}
