using System.Net;
using JobAutomation.Domain.Exceptions;
using JobAutomation.Infrastructure.Security;

namespace JobAutomation.UnitTests.Security;

public class SsrTargetValidatorTests
{
    private readonly SsrTargetValidator _validator = new();

    [Theory]
    [InlineData("http://localhost/api")]
    [InlineData("http://127.0.0.1/api")]
    [InlineData("http://0.0.0.0/")]
    [InlineData("http://[::1]/")]
    [InlineData("http://10.0.0.1/")]
    [InlineData("http://172.16.0.1/")]
    [InlineData("http://192.168.1.1/")]
    [InlineData("http://169.254.169.254/latest/meta-data/")]
    public async Task ValidateAsync_BlockedTargets_Throws(string url)
    {
        var uri = new Uri(url);
        await Assert.ThrowsAsync<SsrValidationException>(() => _validator.ValidateAsync(uri));
    }

    [Fact]
    public void IsBlockedAddress_PublicIp_ReturnsFalse()
    {
        Assert.False(SsrTargetValidator.IsBlockedAddress(IPAddress.Parse("8.8.8.8")));
    }

    [Fact]
    public void IsBlockedAddress_MetadataIp_ReturnsTrue()
    {
        Assert.True(SsrTargetValidator.IsBlockedAddress(IPAddress.Parse("169.254.169.254")));
    }
}
