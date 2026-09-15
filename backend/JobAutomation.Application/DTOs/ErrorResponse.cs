namespace JobAutomation.Application.DTOs;

public sealed class ErrorResponse
{
    public required ErrorDetail Error { get; init; }
}

public sealed class ErrorDetail
{
    public required string Code { get; init; }

    public required string Message { get; init; }

    public string? RequestId { get; init; }

    public IReadOnlyDictionary<string, string[]>? Fields { get; init; }
}
