using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AuditX.Api.IntegrationTests;

/// <summary>
/// P2-C typed execution procedures: recording sampling / interview / walkthrough against an audit, listing +
/// deleting them, sampling-count guards, permission gating, and the procedure-summary analytics.
/// </summary>
public sealed class ProcedureFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
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

    private static string Version(JsonElement node) => node.GetProperty("version").GetString()!;

    private static async Task<Dictionary<string, Guid>> UsersByEmailAsync(HttpClient admin)
    {
        var data = await DataAsync(await admin.GetAsync("/api/v1/users?limit=100"));
        return data.GetProperty("items").EnumerateArray()
            .ToDictionary(u => u.GetProperty("email").GetString()!, u => u.GetProperty("id").GetGuid());
    }

    private async Task<HttpClient> AnalystAsync(HttpClient admin)
    {
        var users = await UsersByEmailAsync(admin);
        var roles = await DataAsync(await admin.GetAsync("/api/v1/roles"));
        var roleId = roles.EnumerateArray().First(r => r.GetProperty("name").GetString() == "Audit Manager").GetProperty("id").GetGuid();
        await admin.PostAsJsonAsync($"/api/v1/users/{users["manager@auditx.local"]}/roles", new { roleId, scopeValue = (string?)null });
        return await LoginAsync("manager");
    }

    private async Task<(Guid AuditId, Guid ItemId)> SeedInProgressAuditAsync(HttpClient admin)
    {
        var users = await UsersByEmailAsync(admin);
        var created = await DataAsync(await admin.PostAsJsonAsync("/api/v1/audits", new
        {
            name = $"Proc {Guid.NewGuid():N}",
            auditType = "branch",
            startDate = "2027-01-10",
            targetEndDate = "2027-02-10",
            leadUserId = users["manager@auditx.local"],
            auditeeUserId = users["auditee@auditx.local"],
        }));
        var auditId = created.GetProperty("id").GetGuid();
        var version = Version(created);

        var withItem = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/checklist/items",
            new { prompt = "Q0", responseType = "pass_fail_na", isRequired = true, version }));
        version = Version(withItem);
        var itemId = withItem.GetProperty("checklistItems").EnumerateArray().First().GetProperty("id").GetGuid();
        var withTeam = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/team",
            new { userId = users["auditor@auditx.local"], teamRole = "auditor", version }));
        version = Version(withTeam);
        var planned = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition", new { targetState = "planned", reason = (string?)null, version }));
        version = Version(planned);
        await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition", new { targetState = "in_progress", reason = (string?)null, version }));
        return (auditId, itemId);
    }

    [Fact]
    public async Task Record_list_delete_and_procedure_analytics()
    {
        var admin = await LoginAsync("admin"); // Administrator: RespondItem + ManageAudit
        var (auditId, itemId) = await SeedInProgressAuditAsync(admin);

        // Sampling procedure with the numeric test fields.
        var sampling = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/procedures", new
        {
            type = "sampling", checklistItemId = itemId, performedOn = "2027-01-20",
            summary = "Tested 25 of 300 payment vouchers for dual authorisation.",
            population = 300, sampleSize = 25, itemsTested = 25, exceptionsFound = 2, method = "random",
        }));
        Assert.Equal("sampling", sampling.GetProperty("type").GetString());
        Assert.Equal(300, sampling.GetProperty("population").GetInt32());
        Assert.Equal(2, sampling.GetProperty("exceptionsFound").GetInt32());

        // Interview procedure (numerics ignored).
        var interview = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/procedures", new
        {
            type = "interview", performedOn = "2027-01-21",
            summary = "Walked the reconciliation process with the branch manager.", counterparty = "J. Doe, Branch Manager",
        }));
        Assert.True(interview.GetProperty("population").ValueKind == JsonValueKind.Null);

        // Both appear in the per-audit list.
        var list = await DataAsync(await admin.GetAsync($"/api/v1/audits/{auditId}/procedures"));
        Assert.Equal(2, list.GetArrayLength());

        // Delete the interview.
        var del = await admin.DeleteAsync($"/api/v1/procedures/{interview.GetProperty("id").GetGuid()}?version={Uri.EscapeDataString(Version(interview))}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);
        var after = await DataAsync(await admin.GetAsync($"/api/v1/audits/{auditId}/procedures"));
        Assert.Equal(1, after.GetArrayLength());

        // Procedure analytics reflect the sampling.
        var analyst = await AnalystAsync(admin);
        var summary = await DataAsync(await analyst.GetAsync("/api/v1/analytics/procedures"));
        Assert.True(summary.GetProperty("totalProcedures").GetInt32() >= 1);
        Assert.True(summary.GetProperty("totalItemsTested").GetInt32() >= 25);
        Assert.True(summary.GetProperty("totalExceptionsFound").GetInt32() >= 2);
    }

    [Fact]
    public async Task Sampling_count_guards_are_enforced()
    {
        var admin = await LoginAsync("admin");
        var (auditId, _) = await SeedInProgressAuditAsync(admin);

        // Sample size cannot exceed the population.
        var bad = await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/procedures", new
        {
            type = "sampling", performedOn = "2027-01-20", summary = "Invalid sample.",
            population = 10, sampleSize = 20, itemsTested = 20, exceptionsFound = 0, method = "random",
        });
        Assert.Equal(HttpStatusCode.UnprocessableContent, bad.StatusCode);
    }

    [Fact]
    public async Task Recording_a_procedure_requires_the_respond_permission()
    {
        var admin = await LoginAsync("admin");
        var (auditId, _) = await SeedInProgressAuditAsync(admin);

        var auditee = await LoginAsync("auditee"); // no RespondItem
        var resp = await auditee.PostAsJsonAsync($"/api/v1/audits/{auditId}/procedures", new
        {
            type = "walkthrough", performedOn = "2027-01-20", summary = "Auditee should not be able to record procedures.",
        });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }
}
