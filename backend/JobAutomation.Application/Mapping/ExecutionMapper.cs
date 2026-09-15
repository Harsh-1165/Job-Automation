using JobAutomation.Application.DTOs.Executions;
using JobAutomation.Domain.Entities;

namespace JobAutomation.Application.Mapping;

public static class ExecutionMapper
{
    public static RunJobResponse ToRunResponse(Execution execution, bool isNewlyCreated = true) => new()
    {
        Id = execution.Id,
        JobId = execution.JobId,
        Status = execution.Status,
        TriggerType = execution.TriggerType,
        Attempt = execution.AttemptNumber,
        CreatedAtUtc = execution.CreatedAtUtc,
        IsNewlyCreated = isNewlyCreated
    };

    public static ExecutionSummaryResponse ToSummary(Execution execution) => new()
    {
        Id = execution.Id,
        JobId = execution.JobId,
        Status = execution.Status,
        TriggerType = execution.TriggerType,
        Attempt = execution.AttemptNumber,
        HttpStatusCode = execution.HttpStatusCode,
        DurationMs = ComputeDurationMs(execution),
        CreatedAtUtc = execution.CreatedAtUtc
    };

    public static ExecutionResponse ToDetail(Execution execution) => new()
    {
        Id = execution.Id,
        JobId = execution.JobId,
        JobName = execution.Job?.Name ?? string.Empty,
        Status = execution.Status,
        TriggerType = execution.TriggerType,
        Attempt = execution.AttemptNumber,
        MaxRetries = execution.Job?.MaxRetries ?? 0,
        NextRetryAtUtc = execution.NextRetryAtUtc,
        StartedAtUtc = execution.StartedAtUtc,
        CompletedAtUtc = execution.CompletedAtUtc,
        DurationMs = ComputeDurationMs(execution),
        HttpStatusCode = execution.HttpStatusCode,
        ResponseBody = execution.ResponseBody,
        ErrorMessage = execution.ErrorMessage,
        CreatedAtUtc = execution.CreatedAtUtc,
        Logs = execution.Logs
            .OrderBy(l => l.CreatedAtUtc)
            .Select(l => new ExecutionLogResponse
            {
                Id = l.Id,
                Level = l.Level,
                Message = l.Message,
                CreatedAtUtc = l.CreatedAtUtc
            })
            .ToList()
    };

    public static long? ComputeDurationMs(Execution execution)
    {
        if (execution.StartedAtUtc is null || execution.CompletedAtUtc is null)
        {
            return null;
        }

        return (long)(execution.CompletedAtUtc.Value - execution.StartedAtUtc.Value).TotalMilliseconds;
    }
}
