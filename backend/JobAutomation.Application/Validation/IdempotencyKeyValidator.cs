using JobAutomation.Domain.Exceptions;

namespace JobAutomation.Application.Validation;

public static class IdempotencyKeyValidator
{
    private const int MaxLength = 36;

    public static string ValidateAndNormalize(string? idempotencyKey)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ValidationException("Validation failed.", new Dictionary<string, string[]>
            {
                ["idempotencyKey"] = ["Idempotency-Key header is required."]
            });
        }

        var trimmed = idempotencyKey.Trim();

        if (trimmed.Length > MaxLength)
        {
            throw new ValidationException("Validation failed.", new Dictionary<string, string[]>
            {
                ["idempotencyKey"] = [$"Idempotency-Key must not exceed {MaxLength} characters."]
            });
        }

        if (!Guid.TryParse(trimmed, out _))
        {
            throw new ValidationException("Validation failed.", new Dictionary<string, string[]>
            {
                ["idempotencyKey"] = ["Idempotency-Key must be a valid GUID."]
            });
        }

        return trimmed.ToLowerInvariant();
    }
}
