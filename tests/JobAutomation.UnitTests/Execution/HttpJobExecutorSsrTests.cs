using System.Net;
using JobAutomation.Domain.Entities;
using JobAutomation.Domain.Enums;
using JobAutomation.Infrastructure.HttpExecution;
using JobAutomation.Infrastructure.Security;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace JobAutomation.UnitTests.HttpExecution;

public class HttpJobExecutorSsrTests
{
    [Fact]
    public async Task ExecuteAsync_LocalhostTarget_IsRejected()
    {
        var executor = CreateExecutor(new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)));
        var result = await executor.ExecuteAsync(CreateJob("http://127.0.0.1/private"));

        Assert.False(result.Succeeded);
        Assert.Equal(Application.ExecutionFailureType.RequestConstruction, result.FailureType);
    }

    [Fact]
    public async Task ExecuteAsync_RedirectToPrivateIp_IsRejected()
    {
        var handler = new StubHandler(req =>
        {
            if (req.RequestUri!.Host.Equals("example.com", StringComparison.OrdinalIgnoreCase))
            {
                return new HttpResponseMessage(HttpStatusCode.Found)
                {
                    Headers = { Location = new Uri("http://127.0.0.1/internal") }
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK);
        });

        var executor = CreateExecutor(handler);
        var result = await executor.ExecuteAsync(CreateJob("https://example.com/start"));

        Assert.False(result.Succeeded);
        Assert.Equal(Application.ExecutionFailureType.RequestConstruction, result.FailureType);
    }

    private static Job CreateJob(string url) => new()
    {
        Id = Guid.NewGuid(),
        UserId = Guid.NewGuid(),
        Name = "SSRF Test",
        HttpMethod = "GET",
        TargetUrl = url,
        TimeoutSeconds = 30,
        Status = JobStatus.Active,
        CreatedAtUtc = DateTime.UtcNow,
        UpdatedAtUtc = DateTime.UtcNow
    };

    private static HttpJobExecutor CreateExecutor(HttpMessageHandler handler) =>
        new(
            new StubHttpClientFactory(handler),
            new SsrTargetValidator(),
            Options.Create(new HttpJobExecutorOptions()),
            NullLogger<HttpJobExecutor>.Instance);

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) => _handler = handler;

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
