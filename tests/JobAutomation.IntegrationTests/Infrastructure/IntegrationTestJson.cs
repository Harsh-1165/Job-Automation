using System.Text.Json;
using System.Text.Json.Serialization;

namespace JobAutomation.IntegrationTests.Infrastructure;

public static class IntegrationTestJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };
}
