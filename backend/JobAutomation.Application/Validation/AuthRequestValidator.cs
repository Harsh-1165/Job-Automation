using System.Net.Mail;
using JobAutomation.Application.DTOs.Auth;
using JobAutomation.Domain.Exceptions;

namespace JobAutomation.Application.Validation;

public static class AuthRequestValidator
{
    private const int MinPasswordLength = 8;
    private const int MaxPasswordLength = 128;

    public static void ValidateRegister(RegisterRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            errors["email"] = ["Email is required."];
        }
        else if (!IsValidEmail(request.Email))
        {
            errors["email"] = ["Email format is invalid."];
        }

        ValidatePassword(request.Password, errors);

        if (errors.Count > 0)
        {
            throw new ValidationException("Validation failed.", errors);
        }
    }

    public static void ValidateLogin(LoginRequest request)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            errors["email"] = ["Email is required."];
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            errors["password"] = ["Password is required."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException("Validation failed.", errors);
        }
    }

    private static void ValidatePassword(string? password, Dictionary<string, string[]> errors)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            errors["password"] = ["Password is required."];
            return;
        }

        if (password.Length < MinPasswordLength)
        {
            errors["password"] = [$"Password must be at least {MinPasswordLength} characters."];
        }
        else if (password.Length > MaxPasswordLength)
        {
            errors["password"] = [$"Password must not exceed {MaxPasswordLength} characters."];
        }
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            _ = new MailAddress(email);
            return email.Contains('@');
        }
        catch
        {
            return false;
        }
    }
}
