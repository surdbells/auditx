using System.Net.Http.Json;
using System.Text.Json;

namespace AuditX.Api.IntegrationTests;

/// <summary>
/// P0-A: department / business-unit analytics. An entity assigned to a child org unit must roll its counts up into
/// the parent's scorecard (subtree aggregation), and the entity create/update commands must persist the assignment.
/// </summary>
public sealed class OrgUnitAnalyticsFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private async Task<HttpClient> LoginAsync(string username)
    {
        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = true });
        (await client.PostAsJsonAsync("/api/v1/auth/login", new { username, password = "Passw0rd!" })).EnsureSuccessStatusCode();
        return client;
    }

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("data").Clone();
    }

    /// <summary>Grant the Audit Manager role (ViewAnalytics) to the seeded 'manager' and return a logged-in client.</summary>
    private async Task<HttpClient> AnalystAsync(HttpClient admin)
    {
        var users = (await DataAsync(await admin.GetAsync("/api/v1/users?limit=100")))
            .GetProperty("items").EnumerateArray()
            .ToDictionary(u => u.GetProperty("email").GetString()!, u => u.GetProperty("id").GetGuid());
        var roles = await DataAsync(await admin.GetAsync("/api/v1/roles"));
        var roleId = roles.EnumerateArray().First(r => r.GetProperty("name").GetString() == "Audit Manager").GetProperty("id").GetGuid();
        await admin.PostAsJsonAsync($"/api/v1/users/{users["manager@auditx.local"]}/roles", new { roleId, scopeValue = (string?)null });
        return await LoginAsync("manager");
    }

    [Fact]
    public async Task Entity_org_assignment_rolls_up_into_the_parent_scorecard()
    {
        var admin = await LoginAsync("admin"); // Administrator: ManageUniverse (creates org units + entities)
        var analyst = await AnalystAsync(admin); // Audit Manager: ViewAnalytics (reads the scorecards)

        var entityTypes = await DataAsync(await admin.GetAsync("/api/v1/audit-universe/entity-types"));
        var entityType = entityTypes.EnumerateArray().First().GetString();

        var suffix = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var head = await DataAsync(await admin.PostAsJsonAsync("/api/v1/org-units",
            new { name = $"HQ {suffix}", code = $"HQ{suffix}", parentOrgUnitId = (Guid?)null }));
        var headId = head.GetProperty("id").GetGuid();

        var division = await DataAsync(await admin.PostAsJsonAsync("/api/v1/org-units",
            new { name = $"Division {suffix}", code = $"DIV{suffix}", parentOrgUnitId = headId }));
        var divisionId = division.GetProperty("id").GetGuid();

        // Create an entity assigned to the CHILD division.
        var entity = await DataAsync(await admin.PostAsJsonAsync("/api/v1/audit-universe/entities",
            new { name = $"Org test entity {suffix}", entityType, orgUnitId = divisionId }));
        Assert.Equal(divisionId, entity.GetProperty("orgUnitId").GetGuid());

        var cards = await DataAsync(await analyst.GetAsync("/api/v1/analytics/org-units"));

        var divisionCard = cards.EnumerateArray().Single(c => c.GetProperty("orgUnitId").GetGuid() == divisionId);
        Assert.True(divisionCard.GetProperty("entities").GetInt32() >= 1);

        // The parent HQ rolls up the child division's entity (subtree aggregation).
        var headCard = cards.EnumerateArray().Single(c => c.GetProperty("orgUnitId").GetGuid() == headId);
        Assert.True(headCard.GetProperty("entities").GetInt32() >= 1, "the parent should include the child's entity");
        Assert.Equal(0, headCard.GetProperty("depth").GetInt32());
        Assert.Equal(1, divisionCard.GetProperty("depth").GetInt32());
    }

    [Fact]
    public async Task Org_scorecards_require_the_analytics_permission()
    {
        var auditee = await LoginAsync("auditee"); // no analytics permission
        var response = await auditee.GetAsync("/api/v1/analytics/org-units");
        Assert.Equal(System.Net.HttpStatusCode.Forbidden, response.StatusCode);
    }
}
