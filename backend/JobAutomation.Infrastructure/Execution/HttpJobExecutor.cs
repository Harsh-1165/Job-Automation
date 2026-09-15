using System.Globalization;

using System.Net;

using System.Net.Http.Headers;

using System.Text;

using System.Text.Json;

using JobAutomation.Application;

using JobAutomation.Application.Interfaces;

using JobAutomation.Domain.Entities;

using JobAutomation.Domain.Exceptions;

using Microsoft.Extensions.Logging;

using Microsoft.Extensions.Options;



namespace JobAutomation.Infrastructure.HttpExecution;



public class HttpJobExecutor : IHttpJobExecutor

{

    private readonly IHttpClientFactory _httpClientFactory;

    private readonly ISsrTargetValidator _ssrTargetValidator;

    private readonly HttpJobExecutorOptions _options;

    private readonly ILogger<HttpJobExecutor> _logger;



    public HttpJobExecutor(

        IHttpClientFactory httpClientFactory,

        ISsrTargetValidator ssrTargetValidator,

        IOptions<HttpJobExecutorOptions> options,

        ILogger<HttpJobExecutor> logger)

    {

        _httpClientFactory = httpClientFactory;

        _ssrTargetValidator = ssrTargetValidator;

        _options = options.Value;

        _logger = logger;

    }



    public async Task<HttpJobExecutionResult> ExecuteAsync(

        Job job,

        CancellationToken cancellationToken = default)

    {

        if (!Uri.TryCreate(job.TargetUrl, UriKind.Absolute, out var initialUri))

        {

            return RequestConstructionFailure("Job URL is invalid.");

        }



        try

        {

            await _ssrTargetValidator.ValidateAsync(initialUri, cancellationToken);

        }

        catch (SsrValidationException)

        {

            return RequestConstructionFailure("The target URL is not allowed.");

        }



        var method = new HttpMethod(job.HttpMethod.ToUpperInvariant());

        var headers = DeserializeHeaders(job.RequestHeadersJson);

        string? contentType = null;

        string? body = null;



        foreach (var (key, value) in headers)

        {

            if (key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))

            {

                contentType = value;

            }

        }



        if (method != HttpMethod.Get && !string.IsNullOrWhiteSpace(job.RequestBody))

        {

            body = job.RequestBody;

        }



        var client = _httpClientFactory.CreateClient("JobExecutor");



        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        timeoutCts.CancelAfter(TimeSpan.FromSeconds(job.TimeoutSeconds));



        try

        {

            var currentUri = initialUri;

            var currentMethod = method;

            HttpResponseMessage? response = null;



            for (var redirectCount = 0; redirectCount <= _options.MaxRedirects; redirectCount++)

            {

                try

                {

                    await _ssrTargetValidator.ValidateAsync(currentUri, timeoutCts.Token);

                }

                catch (SsrValidationException)

                {

                    return RequestConstructionFailure("Redirect target URL is not allowed.");

                }



                using var request = BuildRequest(currentUri, currentMethod, headers, contentType, body, redirectCount == 0);



                response?.Dispose();

                response = await client.SendAsync(

                    request,

                    HttpCompletionOption.ResponseHeadersRead,

                    timeoutCts.Token);



                if (!IsRedirectStatusCode(response.StatusCode))

                {

                    break;

                }



                if (redirectCount >= _options.MaxRedirects)

                {

                    return RequestConstructionFailure("Too many redirects.");

                }



                var location = response.Headers.Location;

                if (location is null)

                {

                    return RequestConstructionFailure("Redirect response missing Location header.");

                }



                currentUri = location.IsAbsoluteUri ? location : new Uri(currentUri, location);

                currentMethod = response.StatusCode is HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect

                    ? currentMethod

                    : HttpMethod.Get;

                body = null;

            }



            if (response is null)
            {
                return RequestConstructionFailure("No HTTP response was received.");
            }

            using (response)
            {
                var responseBody = await ReadResponseBodyAsync(response, timeoutCts.Token);

                var statusCode = (int)response.StatusCode;

                var succeeded = response.IsSuccessStatusCode;

                var retryAfterSeconds = statusCode == 429

                    ? ParseRetryAfterSeconds(response.Headers.RetryAfter)

                    : null;



                return new HttpJobExecutionResult

                {

                    Succeeded = succeeded,

                    HttpStatusCode = statusCode,

                    ResponseBody = responseBody,

                    FailureType = succeeded ? ExecutionFailureType.None : ExecutionFailureType.HttpResponse,

                    RetryAfterSeconds = retryAfterSeconds,

                    ErrorMessage = succeeded

                        ? null

                        : $"External service returned HTTP {statusCode}."

                };

            }

        }

        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)

        {

            return new HttpJobExecutionResult

            {

                Succeeded = false,

                FailureType = ExecutionFailureType.Timeout,

                ErrorMessage = "Request timeout."

            };

        }

        catch (HttpRequestException ex)

        {

            _logger.LogWarning(ex, "HTTP request failed for job {JobId}", job.Id);

            return new HttpJobExecutionResult

            {

                Succeeded = false,

                FailureType = ExecutionFailureType.Network,

                ErrorMessage = "External request failed: connection could not be established."

            };

        }

    }



    internal static int? ParseRetryAfterSeconds(RetryConditionHeaderValue? retryAfter)

    {

        if (retryAfter is null)

        {

            return null;

        }



        if (retryAfter.Delta is { } delta)

        {

            var seconds = (int)Math.Ceiling(delta.TotalSeconds);

            return seconds > 0 ? seconds : null;

        }



        if (retryAfter.Date is { } date)

        {

            var seconds = (int)Math.Ceiling((date - DateTimeOffset.UtcNow).TotalSeconds);

            return seconds > 0 ? seconds : null;

        }



        return null;

    }



    private static HttpRequestMessage BuildRequest(

        Uri uri,

        HttpMethod method,

        Dictionary<string, string> headers,

        string? contentType,

        string? body,

        bool includeBody)

    {

        var request = new HttpRequestMessage(method, uri);



        foreach (var (key, value) in headers)

        {

            if (key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase))

            {

                continue;

            }



            if (!request.Headers.TryAddWithoutValidation(key, value))

            {

                request.Content ??= new StringContent(string.Empty);

                request.Content.Headers.TryAddWithoutValidation(key, value);

            }

        }



        if (includeBody && method != HttpMethod.Get && !string.IsNullOrWhiteSpace(body))

        {

            request.Content = new StringContent(

                body,

                Encoding.UTF8,

                contentType ?? "application/json");

        }



        return request;

    }



    private static bool IsRedirectStatusCode(HttpStatusCode statusCode) =>

        statusCode is HttpStatusCode.MovedPermanently

            or HttpStatusCode.Found

            or HttpStatusCode.SeeOther

            or HttpStatusCode.TemporaryRedirect

            or HttpStatusCode.PermanentRedirect;



    private static HttpJobExecutionResult RequestConstructionFailure(string message) =>

        new()

        {

            Succeeded = false,

            FailureType = ExecutionFailureType.RequestConstruction,

            ErrorMessage = message

        };



    private async Task<string?> ReadResponseBodyAsync(

        HttpResponseMessage response,

        CancellationToken cancellationToken)

    {

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);



        if (bytes.Length == 0)

        {

            return null;

        }



        if (bytes.Length <= _options.MaxResponseBodyBytes)

        {

            return Encoding.UTF8.GetString(bytes);

        }



        var truncated = bytes.AsSpan(0, _options.MaxResponseBodyBytes).ToArray();

        return Encoding.UTF8.GetString(truncated) + "\n[Response truncated after 1 MB]";

    }



    private static Dictionary<string, string> DeserializeHeaders(string? json)

    {

        if (string.IsNullOrWhiteSpace(json))

        {

            return new Dictionary<string, string>();

        }



        return JsonSerializer.Deserialize<Dictionary<string, string>>(json, new JsonSerializerOptions

        {

            PropertyNamingPolicy = JsonNamingPolicy.CamelCase

        }) ?? new Dictionary<string, string>();

    }

}

