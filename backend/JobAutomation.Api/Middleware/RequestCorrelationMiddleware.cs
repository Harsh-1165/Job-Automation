using System.Diagnostics;

namespace JobAutomation.Api.Middleware;

public class RequestCorrelationMiddleware
{
    public const string HeaderName = "X-Request-Id";

    private readonly RequestDelegate _next;
    private readonly ILogger<RequestCorrelationMiddleware> _logger;

    public RequestCorrelationMiddleware(
        RequestDelegate next,
        ILogger<RequestCorrelationMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var requestId = ResolveRequestId(context.Request.Headers[HeaderName].FirstOrDefault());
        context.TraceIdentifier = requestId;
        context.Response.Headers[HeaderName] = requestId;

        using (_logger.BeginScope(new Dictionary<string, object>
        {
            ["RequestId"] = requestId
        }))
        {
            Activity.Current?.SetTag("request.id", requestId);
            await _next(context);
        }
    }

    private static string ResolveRequestId(string? incoming)
    {
        if (!string.IsNullOrWhiteSpace(incoming)
            && Guid.TryParse(incoming.Trim(), out var parsed))
        {
            return parsed.ToString("D");
        }

        return Guid.NewGuid().ToString("D");
    }
}
