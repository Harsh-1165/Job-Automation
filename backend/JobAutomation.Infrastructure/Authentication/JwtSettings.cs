namespace JobAutomation.Infrastructure.Authentication;

public class JwtSettings
{
    public const string SectionName = "Jwt";

    public string Secret { get; set; } = string.Empty;

    public string Issuer { get; set; } = "JobAutomation";

    public string Audience { get; set; } = "JobAutomation";

    public int ExpirationMinutes { get; set; } = 60;
}
