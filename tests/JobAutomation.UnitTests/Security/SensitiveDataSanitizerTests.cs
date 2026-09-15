using JobAutomation.Application.Security;

namespace JobAutomation.UnitTests.Security;

public class SensitiveDataSanitizerTests
{
    [Fact]
    public void SanitizeHeaders_RedactsAuthorization()
    {
        var headers = new Dictionary<string, string>
        {
            ["Authorization"] = "Bearer secret-token",
            ["Accept"] = "application/json"
        };

        var sanitized = SensitiveDataSanitizer.SanitizeHeaders(headers);

        Assert.Equal(SensitiveDataSanitizer.RedactedValue, sanitized!["Authorization"]);
        Assert.Equal("application/json", sanitized["Accept"]);
    }

    [Fact]
    public void SanitizeForLog_RedactsBearerToken()
    {
        var sanitized = SensitiveDataSanitizer.SanitizeForLog("Bearer abc.def.ghi");
        Assert.Equal("Bearer [REDACTED]", sanitized);
    }
}
