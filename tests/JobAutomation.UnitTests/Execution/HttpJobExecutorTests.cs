using System.Net;
using JobAutomation.Domain.Entities;
using JobAutomation.Domain.Enums;
using JobAutomation.Infrastructure.HttpExecution;
using JobAutomation.Infrastructure.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace JobAutomation.UnitTests.HttpExecution;

public class HttpJobExecutorTests
{
    private static Job CreateJob(
        string method = "GET",
        string url = "https://example.com/api",
        string? body = null,
        string? headersJson = null,
        int timeoutSeconds = 30) => new()
    {
        Id = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        Name = "Test",
        HttpMethod = method,
        TargetUrl = url,
        RequestBody = body,
        RequestHeadersJson = headersJson,
        TimeoutSeconds = timeoutSeconds,
        Status = JobStatus.Active,
        CreatedAtUtc = DateTime.UtcNow,
        UpdatedAtUtc = DateTime.UtcNow
    };

    [Fact]
    public async Task ExecuteAsync_Get2xx_Succeeds()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"ok\":true}")
        });

        var executor = CreateExecutor(handler);
        var result = await executor.ExecuteAsync(CreateJob());

        Assert.True(result.Succeeded);
        Assert.Equal(200, result.HttpStatusCode);
        Assert.Contains("ok", result.ResponseBody);
    }

    [Fact]
    public async Task ExecuteAsync_Post_SendsBody()
    {
        string? capturedBody = null;
        HttpMethod? capturedMethod = null;
        var handler = new StubHandler(req =>
        {
            capturedMethod = req.Method;
            capturedBody = req.Content is null
                ? null
                : req.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.Created);
        });

        var executor = CreateExecutor(handler);
        var job = CreateJob("POST", "https://example.com/hook", body: "{\"event\":\"test\"}");

        var result = await executor.ExecuteAsync(job);

        Assert.True(result.Succeeded);
        Assert.Equal(201, result.HttpStatusCode);
        Assert.Equal(HttpMethod.Post, capturedMethod);
        Assert.Equal("{\"event\":\"test\"}", capturedBody);
    }

    [Fact]
    public async Task ExecuteAsync_4xx_Fails()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("bad request")
        });

        var result = await CreateExecutor(handler).ExecuteAsync(CreateJob());

        Assert.False(result.Succeeded);
        Assert.Equal(400, result.HttpStatusCode);
    }

    [Fact]
    public async Task ExecuteAsync_5xx_Fails()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        var result = await CreateExecutor(handler).ExecuteAsync(CreateJob());

        Assert.False(result.Succeeded);
        Assert.Equal(500, result.HttpStatusCode);
    }

    [Fact]
    public async Task ExecuteAsync_NetworkError_Fails()
    {
        var handler = new StubHandler(_ => throw new HttpRequestException("connection refused"));

        var result = await CreateExecutor(handler).ExecuteAsync(CreateJob());

        Assert.False(result.Succeeded);
        Assert.Contains("connection", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_LargeResponse_Truncates()
    {
        var largeBody = new string('x', 1_048_576 + 100);
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(largeBody)
        });

        var result = await CreateExecutor(handler).ExecuteAsync(CreateJob());

        Assert.True(result.Succeeded);
        Assert.Contains("[Response truncated after 1 MB]", result.ResponseBody);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidUrl_Fails()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var job = CreateJob(url: "javascript:alert(1)");

        var result = await CreateExecutor(handler).ExecuteAsync(job);

        Assert.False(result.Succeeded);
    }

    private static HttpJobExecutor CreateExecutor(HttpMessageHandler handler)
    {
        var factory = new StubHttpClientFactory(handler);
        return new HttpJobExecutor(
            factory,
            new SsrTargetValidator(),
            Options.Create(new HttpJobExecutorOptions()),
            NullLogger<HttpJobExecutor>.Instance);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) =>
            _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(_handler(request));
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpMessageHandler _handler;

        public StubHttpClientFactory(HttpMessageHandler handler) => _handler = handler;

        public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
    }
}
