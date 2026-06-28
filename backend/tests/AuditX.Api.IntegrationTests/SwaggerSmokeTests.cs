using System.Net;
using System.Text.Json;

namespace AuditX.Api.IntegrationTests;

/// <summary>Proves the OpenAPI document actually generates (the custom security/enum/error filters run at doc-gen time).</summary>
public sealed class SwaggerSmokeTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Swagger_document_generates_and_describes_the_api()
    {
        var client = factory.CreateClient();
        var response = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement;

        Assert.Equal("AuditX Enterprise API", root.GetProperty("info").GetProperty("title").GetString());
        Assert.True(root.GetProperty("paths").EnumerateObject().Any(), "the spec should describe endpoints");
        // The bearer security scheme is declared.
        Assert.True(root.GetProperty("components").GetProperty("securitySchemes").TryGetProperty("Bearer", out _));
    }
}
