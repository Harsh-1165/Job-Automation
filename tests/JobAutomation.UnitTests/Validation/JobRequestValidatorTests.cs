using JobAutomation.Application.DTOs.Jobs;
using JobAutomation.Application.Validation;
using JobAutomation.Domain.Exceptions;

namespace JobAutomation.UnitTests.Validation;

public class JobRequestValidatorTests
{
    [Fact]
    public void ValidateCreate_InvalidUrl_Throws()
    {
        var request = ValidRequest(url: "javascript:alert(1)");
        Assert.Throws<ValidationException>(() => JobRequestValidator.ValidateCreate(request));
    }

    [Fact]
    public void ValidateCreate_UnsupportedMethod_Throws()
    {
        var request = ValidRequest(httpMethod: "TRACE");
        Assert.Throws<ValidationException>(() => JobRequestValidator.ValidateCreate(request));
    }

    [Fact]
    public void ValidateCreate_InvalidTimeout_Throws()
    {
        var request = ValidRequest(timeoutSeconds: 0);
        Assert.Throws<ValidationException>(() => JobRequestValidator.ValidateCreate(request));
    }

    [Fact]
    public void ValidateCreate_ValidRequest_DoesNotThrow()
    {
        JobRequestValidator.ValidateCreate(ValidRequest());
    }

    private static CreateJobRequest ValidRequest(
        string url = "https://api.example.com/users",
        string httpMethod = "GET",
        int timeoutSeconds = 30) => new()
    {
        Name = "Sync",
        Url = url,
        HttpMethod = httpMethod,
        TimeoutSeconds = timeoutSeconds
    };
}
