using System.Text.Json;
using System.Threading.RateLimiting;
using JobAutomation.Application;
using JobAutomation.Application.DTOs;
using JobAutomation.Api.Middleware;
using Microsoft.AspNetCore.RateLimiting;

namespace JobAutomation.Api.Extensions;

public static class RateLimitingExtensions
{
    public const string AuthPolicy = "auth";
    public const string RunJobPolicy = "run-job";
    public const string RetryPolicy = "retry";

    public static IServiceCollection AddApiRateLimiting(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var options = configuration.GetSection(RateLimitOptions.SectionName).Get<RateLimitOptions>()
            ?? new RateLimitOptions();

        services.AddRateLimiter(limiterOptions =>
        {
            var rateOptions = configuration.GetSection(RateLimitOptions.SectionName).Get<RateLimitOptions>()
                ?? options;

            if (environment.IsEnvironment("Testing") && !rateOptions.EnableInTesting)
            {
                RegisterNoLimitPolicy(limiterOptions, AuthPolicy);
                RegisterNoLimitPolicy(limiterOptions, RunJobPolicy);
                RegisterNoLimitPolicy(limiterOptions, RetryPolicy);
                return;
            }

            limiterOptions.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiterOptions.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.ContentType = "application/json";
                var requestId = context.HttpContext.TraceIdentifier;
                context.HttpContext.Response.Headers[RequestCorrelationMiddleware.HeaderName] = requestId;

                var response = new ErrorResponse
                {
                    Error = new ErrorDetail
                    {
                        Code = "RATE_LIMITED",
                        Message = "Too many requests. Please try again later.",
                        RequestId = requestId
                    }
                };

                await context.HttpContext.Response.WriteAsync(
                    JsonSerializer.Serialize(response, new JsonSerializerOptions
                    {
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                    }),
                    cancellationToken);
            };

            limiterOptions.AddPolicy(AuthPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    GetPartitionKey(httpContext, "auth"),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = rateOptions.AuthPermitLimit,
                        Window = TimeSpan.FromSeconds(rateOptions.AuthWindowSeconds),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));

            limiterOptions.AddPolicy(RunJobPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    GetPartitionKey(httpContext, "run"),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = rateOptions.RunPermitLimit,
                        Window = TimeSpan.FromSeconds(rateOptions.RunWindowSeconds),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));

            limiterOptions.AddPolicy(RetryPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    GetPartitionKey(httpContext, "retry"),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = rateOptions.RetryPermitLimit,
                        Window = TimeSpan.FromSeconds(rateOptions.RetryWindowSeconds),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
        });

        return services;
    }

    private static void RegisterNoLimitPolicy(RateLimiterOptions limiterOptions, string policyName)
    {
        limiterOptions.AddPolicy(policyName, _ => RateLimitPartition.GetNoLimiter(policyName));
    }

    private static string GetPartitionKey(HttpContext httpContext, string prefix)
    {
        var userId = httpContext.User?.FindFirst("sub")?.Value;
        if (!string.IsNullOrWhiteSpace(userId))
        {
            return $"{prefix}:user:{userId}";
        }

        var remoteIp = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return $"{prefix}:ip:{remoteIp}";
    }
}
