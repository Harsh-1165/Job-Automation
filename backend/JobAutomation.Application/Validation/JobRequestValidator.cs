using JobAutomation.Application.DTOs.Jobs;
using JobAutomation.Domain.Exceptions;

namespace JobAutomation.Application.Validation;

public static class JobRequestValidator
{
    private static readonly HashSet<string> AllowedMethods =
        new(StringComparer.OrdinalIgnoreCase) { "GET", "POST", "PUT", "PATCH", "DELETE" };

    private const int MaxNameLength = 200;
    private const int MaxDescriptionLength = 2000;
    private const int MaxBodyLength = 65_536;
    private const int MaxHeaderCount = 20;
    private const int MaxHeaderNameLength = 256;
    private const int MaxHeaderValueLength = 4096;
    private const int MinTimeoutSeconds = 1;
    private const int MaxTimeoutSeconds = 300;

    public static void ValidateCreate(CreateJobRequest request) =>
        ValidateCore(request.Name, request.Description, request.Url, request.HttpMethod,
            request.Headers, request.Body, request.TimeoutSeconds);

    public static void ValidateUpdate(UpdateJobRequest request) =>
        ValidateCore(request.Name, request.Description, request.Url, request.HttpMethod,
            request.Headers, request.Body, request.TimeoutSeconds);

    private static void ValidateCore(
        string? name,
        string? description,
        string? url,
        string? httpMethod,
        Dictionary<string, string>? headers,
        string? body,
        int timeoutSeconds)
    {
        var errors = new Dictionary<string, string[]>();

        if (string.IsNullOrWhiteSpace(name))
        {
            errors["name"] = ["Name is required."];
        }
        else if (name.Length > MaxNameLength)
        {
            errors["name"] = [$"Name must not exceed {MaxNameLength} characters."];
        }

        if (description?.Length > MaxDescriptionLength)
        {
            errors["description"] = [$"Description must not exceed {MaxDescriptionLength} characters."];
        }

        if (string.IsNullOrWhiteSpace(url))
        {
            errors["url"] = ["URL is required."];
        }
        else if (!IsValidHttpUrl(url))
        {
            errors["url"] = ["URL must be a valid absolute http or https URL."];
        }

        if (string.IsNullOrWhiteSpace(httpMethod))
        {
            errors["httpMethod"] = ["HTTP method is required."];
        }
        else if (!AllowedMethods.Contains(httpMethod))
        {
            errors["httpMethod"] = ["HTTP method must be one of: GET, POST, PUT, PATCH, DELETE."];
        }

        if (body?.Length > MaxBodyLength)
        {
            errors["body"] = [$"Body must not exceed {MaxBodyLength} characters."];
        }

        if (headers is not null)
        {
            if (headers.Count > MaxHeaderCount)
            {
                errors["headers"] = [$"No more than {MaxHeaderCount} headers are allowed."];
            }

            foreach (var (key, value) in headers)
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    errors["headers"] = ["Header keys cannot be empty."];
                    break;
                }

                if (key.Length > MaxHeaderNameLength)
                {
                    errors["headers"] = [$"Header name '{key}' exceeds the maximum length."];
                    break;
                }

                if (value is null)
                {
                    errors["headers"] = [$"Header '{key}' must have a value."];
                    break;
                }

                if (value.Length > MaxHeaderValueLength)
                {
                    errors["headers"] = [$"Header '{key}' exceeds the maximum value length."];
                    break;
                }
            }
        }

        if (timeoutSeconds < MinTimeoutSeconds || timeoutSeconds > MaxTimeoutSeconds)
        {
            errors["timeoutSeconds"] =
                [$"Timeout must be between {MinTimeoutSeconds} and {MaxTimeoutSeconds} seconds."];
        }

        if (errors.Count > 0)
        {
            throw new ValidationException("Validation failed.", errors);
        }
    }

    private static bool IsValidHttpUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.Scheme is "http" or "https";
    }
}
