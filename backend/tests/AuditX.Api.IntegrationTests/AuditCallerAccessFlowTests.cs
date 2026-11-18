using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AuditX.Api.IntegrationTests;

/// <summary>
/// GET audits/{id}/access tells each caller what the audit's screens may offer them, by the same rules the endpoints
/// enforce: a viewer who is neither on the team nor a manager of the audit cannot open its working papers (and the
/// endpoints still refuse them), the audit's auditee may read them but never acts as its auditor, and a manager who
/// is not on the team may.
/// </summary>
public sealed class AuditCallerAccessFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private async Task<HttpClient> LoginAsync(string username)
    {
        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = true });
        (await client.PostAsJsonAsync("/api/v1/auth/login", new { username, password = "Passw0rd!" })).EnsureSuccessStatusCode();
        return client;
    }

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode} from {response.RequestMessage?.RequestUri?.PathAndQuery}: {body}");
        return JsonDocument.Parse(body).RootElement.GetProperty("data").Clone();
    }

    private static async Task<(bool OnTeam, bool IsAuditee, bool ManagesAudit, bool CanSee, bool CanAct)> AccessAsync(HttpClient client, Guid auditId)
    {
        var a = await DataAsync(await client.GetAsync($"/api/v1/audits/{auditId}/access"));
        return (a.GetProperty("onTeam").GetBoolean(), a.GetProperty("isAuditee").GetBoolean(), a.GetProperty("managesAudit").GetBoolean(),
            a.GetProperty("canSeeWorkingPapers").GetBoolean(), a.GetProperty("canActAsAuditor").GetBoolean());
    }

    [Fact]
    public async Task Each_caller_is_told_what_the_audit_lets_them_open_and_the_endpoints_agree()
    {
        var admin = await LoginAsync("admin");
        var users = (await DataAsync(await admin.GetAsync("/api/v1/users?limit=200"))).GetProperty("items").EnumerateArray()
            .ToDictionary(u => u.GetProperty("email").GetString()!, u => u.GetProperty("id").GetGuid());
        var created = await DataAsync(await admin.PostAsJsonAsync("/api/v1/audits", new
        {
            name = $"Access {Guid.NewGuid():N}", auditType = "operational", startDate = "2027-01-10", targetEndDate = "2027-02-10",
            leadUserId = users["manager@auditx.local"], auditeeUserId = users["auditee@auditx.local"],
        }));
        var auditId = created.GetProperty("id").GetGuid();

        // The lead: on the team, so the working papers are theirs to see and work. (Whether their ManageAudit grant
        // reaches this audit depends on the org units it is limited to — not what this test is about.)
        var lead = await AccessAsync(await LoginAsync("manager"), auditId);
        Assert.True(lead.OnTeam);
        Assert.False(lead.IsAuditee);
        Assert.True(lead.CanSee);
        Assert.True(lead.CanAct);

        // An auditor who can see every audit (the built-in Auditor role: ViewAudits, no ManageAudit) but is not on
        // this one's team.
        var auditorRoleId = (await DataAsync(await admin.GetAsync("/api/v1/roles"))).EnumerateArray()
            .First(r => r.GetProperty("name").GetString() == "Auditor").GetProperty("id").GetGuid();
        await admin.PostAsJsonAsync($"/api/v1/users/{users["auditor@auditx.local"]}/roles", new { roleId = auditorRoleId, scopeValue = (string?)null });
        var outsider = await LoginAsync("auditor");
        Assert.Equal((false, false, false, false, false), await AccessAsync(outsider, auditId));
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync($"/api/v1/audits/{auditId}/procedures")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await outsider.GetAsync($"/api/v1/audits/{auditId}/evidence-requests")).StatusCode);

        // The auditee reads the audit's working papers as a member of it, but is never its auditor.
        Assert.Equal((false, true, false, true, false), await AccessAsync(await LoginAsync("auditee"), auditId));

        // An administrator off the team holds ManageAudit everywhere, so the working papers are open to them.
        Assert.Equal((false, false, true, true, true), await AccessAsync(admin, auditId));
        Assert.Equal(HttpStatusCode.OK, (await admin.GetAsync($"/api/v1/audits/{auditId}/procedures")).StatusCode);
    }
}
