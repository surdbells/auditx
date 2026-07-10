using System.Net.Http.Json;
using System.Text.Json;

namespace AuditX.Api.IntegrationTests;

/// <summary>
/// Codebase-wide correctness fix: repository search filters must treat SQL LIKE metacharacters ('%', '_', '[') in the
/// user's term LITERALLY (via <c>col.Contains(term)</c>, which EF translates to LIKE with an ESCAPE clause) rather
/// than as pattern operators. Exercised through the audit-name search (AuditRepository).
/// </summary>
public sealed class SearchWildcardEscapingTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private HttpClient NewClient() => factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
    {
        HandleCookies = true,
    });

    private async Task<HttpClient> LoginAsync(string username)
    {
        var client = NewClient();
        (await client.PostAsJsonAsync("/api/v1/auth/login", new { username, password = "Passw0rd!" })).EnsureSuccessStatusCode();
        return client;
    }

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("data").Clone();
    }

    [Fact]
    public async Task Underscore_in_a_search_term_matches_literally_not_as_a_wildcard()
    {
        var admin = await LoginAsync("admin");
        var users = (await DataAsync(await admin.GetAsync("/api/v1/users?limit=100"))).GetProperty("items").EnumerateArray()
            .ToDictionary(u => u.GetProperty("email").GetString()!, u => u.GetProperty("id").GetGuid());
        var lead = users["manager@auditx.local"];
        var auditee = users["auditee@auditx.local"];
        var tag = Guid.NewGuid().ToString("N")[..8];

        async Task Create(string name) => (await admin.PostAsJsonAsync("/api/v1/audits", new
        {
            name, auditType = "branch", startDate = "2027-01-10", targetEndDate = "2027-02-10", leadUserId = lead, auditeeUserId = auditee,
        })).EnsureSuccessStatusCode();

        // Two audits differing only where '_' vs an arbitrary char sits. A raw LIKE '%..._...%' would match BOTH
        // ('_' is "any single char"); an escaped Contains matches only the literal underscore.
        await Create($"WX{tag} AML_KYC controls");
        await Create($"WX{tag} AMLxKYC controls");

        var results = (await DataAsync(await admin.GetAsync($"/api/v1/audits?search=WX{tag}%20AML_KYC&limit=50")))
            .GetProperty("items").EnumerateArray().Select(a => a.GetProperty("name").GetString()!).ToList();

        Assert.Contains(results, n => n.Contains("AML_KYC"));
        Assert.DoesNotContain(results, n => n.Contains("AMLxKYC"));
    }
}
