using JobAutomation.Application.DTOs.Auth;
using JobAutomation.Application.Validation;
using JobAutomation.Domain.Exceptions;

namespace JobAutomation.UnitTests.Validation;

public class AuthRequestValidatorTests
{
    [Fact]
    public void ValidateRegister_ShortPassword_Throws()
    {
        var request = new RegisterRequest { Email = "user@example.com", Password = "short" };
        Assert.Throws<ValidationException>(() => AuthRequestValidator.ValidateRegister(request));
    }

    [Fact]
    public void ValidateRegister_InvalidEmail_Throws()
    {
        var request = new RegisterRequest { Email = "not-an-email", Password = "password123" };
        Assert.Throws<ValidationException>(() => AuthRequestValidator.ValidateRegister(request));
    }
}
