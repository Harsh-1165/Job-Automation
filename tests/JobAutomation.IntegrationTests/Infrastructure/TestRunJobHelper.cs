namespace JobAutomation.IntegrationTests.Infrastructure;

public static class TestRunJobHelper
{
    public static HttpRequestMessage CreateRunRequest(Guid jobId, Guid? idempotencyKey = null)
    {
        var key = (idempotencyKey ?? Guid.NewGuid()).ToString();
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/jobs/{jobId}/run");
        request.Headers.TryAddWithoutValidation("Idempotency-Key", key);
        return request;
    }
}
