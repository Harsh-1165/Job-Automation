using System.Net;
using System.Text.Json;
using JobAutomation.Application.DTOs;
using JobAutomation.Domain.Exceptions;

namespace JobAutomation.Api.Middleware;

public class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public GlobalExceptionHandlerMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlerMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, code, message, fields) = MapException(exception);

        if (statusCode >= (int)HttpStatusCode.InternalServerError)
        {
            _logger.LogError(exception, "Unhandled exception for {Method} {Path}",
                context.Request.Method, context.Request.Path);
        }
        else
        {
            _logger.LogWarning(exception, "Handled exception for {Method} {Path}: {Code}",
                context.Request.Method, context.Request.Path, code);
        }

        if (!_environment.IsDevelopment())
        {
            message = statusCode >= (int)HttpStatusCode.InternalServerError
                ? "An unexpected error occurred."
                : message;
        }

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = statusCode;

        var response = new ErrorResponse
        {
            Error = new ErrorDetail
            {
                Code = code,
                Message = message,
                RequestId = context.TraceIdentifier,
                Fields = fields
            }
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(response, JsonOptions));
    }

    private static (int StatusCode, string Code, string Message, IReadOnlyDictionary<string, string[]>? Fields)
        MapException(Exception exception)
    {
        return exception switch
        {
            ValidationException validationEx => (
                (int)HttpStatusCode.UnprocessableEntity,
                validationEx.Code,
                validationEx.Message,
                validationEx.Errors),

            BadRequestException badRequestEx => (
                (int)HttpStatusCode.BadRequest,
                badRequestEx.Code,
                badRequestEx.Message,
                badRequestEx.Fields),

            ConflictException conflictEx => (
                (int)HttpStatusCode.Conflict,
                conflictEx.Code,
                conflictEx.Message,
                null),

            UnauthorizedException unauthorizedEx => (
                (int)HttpStatusCode.Unauthorized,
                unauthorizedEx.Code,
                unauthorizedEx.Message,
                null),

            ForbiddenException forbiddenEx => (
                (int)HttpStatusCode.Forbidden,
                forbiddenEx.Code,
                forbiddenEx.Message,
                null),

            SsrValidationException ssrEx => (
                (int)HttpStatusCode.UnprocessableEntity,
                ssrEx.Code,
                ssrEx.Message,
                null),

            NotFoundException notFoundEx => (
                (int)HttpStatusCode.NotFound,
                notFoundEx.Code,
                notFoundEx.Message,
                null),

            DomainException domainEx => (
                (int)HttpStatusCode.BadRequest,
                domainEx.Code,
                domainEx.Message,
                null),

            UnauthorizedAccessException => (
                (int)HttpStatusCode.Unauthorized,
                "UNAUTHORIZED",
                "Authentication is required.",
                null),

            KeyNotFoundException => (
                (int)HttpStatusCode.NotFound,
                "NOT_FOUND",
                "The requested resource was not found.",
                null),

            ArgumentException => (
                (int)HttpStatusCode.BadRequest,
                "INVALID_ARGUMENT",
                exception.Message,
                null),

            _ => (
                (int)HttpStatusCode.InternalServerError,
                "INTERNAL_ERROR",
                exception.Message,
                null)
        };
    }
}
