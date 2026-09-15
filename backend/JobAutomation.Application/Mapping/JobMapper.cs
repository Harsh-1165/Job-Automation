using System.Text.Json;
using JobAutomation.Application.DTOs.Jobs;
using JobAutomation.Application.Security;
using JobAutomation.Domain.Entities;

namespace JobAutomation.Application.Mapping;

public static class JobMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static JobResponse ToResponse(Job job)
    {
        return new JobResponse
        {
            Id = job.Id,
            Name = job.Name,
            Description = job.Description,
            Url = job.TargetUrl,
            HttpMethod = job.HttpMethod,
            Headers = SensitiveDataSanitizer.SanitizeHeaders(DeserializeHeaders(job.RequestHeadersJson)),
            Body = job.RequestBody,
            Schedule = job.CronExpression,
            Status = job.Status,
            TimeoutSeconds = job.TimeoutSeconds,
            CreatedAt = job.CreatedAtUtc,
            UpdatedAt = job.UpdatedAtUtc,
            LastRunAt = job.LastRunAtUtc,
            NextRunAt = job.NextRunAtUtc
        };
    }

    public static string? SerializeHeaders(Dictionary<string, string>? headers)
    {
        if (headers is null || headers.Count == 0)
        {
            return null;
        }

        return JsonSerializer.Serialize(headers, JsonOptions);
    }

    private static Dictionary<string, string>? DeserializeHeaders(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions);
    }
}
