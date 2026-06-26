using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AuditX.Api.IntegrationTests;

public sealed class ExceptionFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private HttpClient NewClient() => factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
    {
        HandleCookies = true,
    });

    private async Task<HttpClient> AdminAsync()
    {
        var client = NewClient();
        (await client.PostAsJsonAsync("/api/v1/auth/login", new { username = "admin", password = "Passw0rd!" })).EnsureSuccessStatusCode();
        return client;
    }

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("data").Clone();
    }

    private static string Version(JsonElement node) => node.GetProperty("version").GetString()!;

    private async Task<string> AuditVersionAsync(HttpClient admin, Guid auditId)
        => Version(await DataAsync(await admin.GetAsync($"/api/v1/audits/{auditId}")));

    private static async Task<Dictionary<string, Guid>> UsersByEmailAsync(HttpClient admin)
    {
        var data = await DataAsync(await admin.GetAsync("/api/v1/users?limit=100"));
        return data.GetProperty("items").EnumerateArray()
            .ToDictionary(u => u.GetProperty("email").GetString()!, u => u.GetProperty("id").GetGuid());
    }

    /// <summary>Build an in-progress audit with two responded items: item0 = Fail, item1 = Pass.</summary>
    private async Task<(Guid AuditId, Guid FailItem, Guid PassItem, Guid Owner)> SeedAuditWithResponsesAsync(HttpClient admin)
    {
        var users = await UsersByEmailAsync(admin);
        var created = await DataAsync(await admin.PostAsJsonAsync("/api/v1/audits", new
        {
            name = $"Exc {Guid.NewGuid():N}",
            auditType = "branch",
            startDate = "2027-01-10",
            targetEndDate = "2027-02-10",
            leadUserId = users["manager@auditx.local"],
            auditeeUserId = users["auditee@auditx.local"],
        }));
        var auditId = created.GetProperty("id").GetGuid();
        var version = Version(created);

        var a1 = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/checklist/items", new { prompt = "Q0", responseType = "pass_fail_na", isRequired = true, version }));
        version = Version(a1);
        var a2 = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/checklist/items", new { prompt = "Q1", responseType = "pass_fail_na", isRequired = true, version }));
        version = Version(a2);
        var withTeam = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/team", new { userId = users["auditor@auditx.local"], teamRole = "auditor", version }));
        version = Version(withTeam);
        var planned = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition", new { targetState = "planned", reason = (string?)null, version }));
        version = Version(planned);
        var started = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition", new { targetState = "in_progress", reason = (string?)null, version }));

        var items = started.GetProperty("checklistItems").EnumerateArray().OrderBy(i => i.GetProperty("orderIndex").GetInt32()).ToArray();
        var failItem = items[0].GetProperty("id").GetGuid();
        var passItem = items[1].GetProperty("id").GetGuid();

        await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/items/{failItem}/responses",
            new { verdict = "fail", comment = "control missing", isDraft = false, version = await AuditVersionAsync(admin, auditId) }));
        await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/items/{passItem}/responses",
            new { verdict = "pass", comment = (string?)null, isDraft = false, version = await AuditVersionAsync(admin, auditId) }));

        return (auditId, failItem, passItem, users["auditee@auditx.local"]);
    }

    [Fact]
    public async Task Raise_is_gated_to_failed_items_and_starts_open()
    {
        var admin = await AdminAsync();
        var (auditId, failItem, passItem, owner) = await SeedAuditWithResponsesAsync(admin);

        // Raising on a Pass item is rejected (US-M5-017).
        var onPass = await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/exceptions", new
        {
            checklistItemId = passItem, title = "Bad", severity = "medium", rootCause = "x", recommendation = "y", ownerUserId = owner,
        });
        Assert.Equal(HttpStatusCode.UnprocessableContent, onPass.StatusCode);

        // Raising on the Fail item succeeds and opens the exception.
        var raised = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/exceptions", new
        {
            checklistItemId = failItem, title = "Segregation gap", severity = "high",
            rootCause = "no maker-checker", recommendation = "introduce dual control", ownerUserId = owner,
        }));
        Assert.Equal("open", raised.GetProperty("status").GetString());
        Assert.Equal("high", raised.GetProperty("severity").GetString());

        // It appears in the per-audit list and the cross-audit tracker.
        var list = await DataAsync(await admin.GetAsync($"/api/v1/audits/{auditId}/exceptions"));
        Assert.Equal(1, list.GetArrayLength());
    }

    [Fact]
    public async Task Map_submit_then_gated_approve_returns_pending_action()
    {
        var admin = await AdminAsync();
        var (auditId, failItem, _, owner) = await SeedAuditWithResponsesAsync(admin);

        var ex = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/exceptions", new
        {
            checklistItemId = failItem, title = "Gap", severity = "medium", rootCause = "rc", recommendation = "rec", ownerUserId = owner,
        }));
        var exId = ex.GetProperty("id").GetGuid();
        var version = Version(ex);

        var submitted = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/exceptions/{exId}/map", new
        {
            actions = new[] { new { description = "Implement dual control", ownerUserId = owner, targetDate = "2027-02-01", expectedEvidenceType = "screenshot" } },
            version,
        }));
        Assert.Equal("map_submitted", submitted.GetProperty("status").GetString());
        version = Version(submitted);

        // map_approval is maker-checker-gated by default → 202 with a pending action id (not executed).
        var approve = await admin.PostAsJsonAsync($"/api/v1/exceptions/{exId}/map/approve", new { version });
        Assert.Equal(HttpStatusCode.Accepted, approve.StatusCode);

        // The MAP is still only submitted (approval captured, not applied).
        var after = await DataAsync(await admin.GetAsync($"/api/v1/exceptions/{exId}"));
        Assert.Equal("map_submitted", after.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Stale_version_on_severity_change_conflicts()
    {
        var admin = await AdminAsync();
        var (auditId, failItem, _, owner) = await SeedAuditWithResponsesAsync(admin);

        var ex = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/exceptions", new
        {
            checklistItemId = failItem, title = "Gap", severity = "low", rootCause = "rc", recommendation = "rec", ownerUserId = owner,
        }));
        var exId = ex.GetProperty("id").GetGuid();
        var stale = Version(ex);

        await DataAsync(await admin.PatchAsJsonAsync($"/api/v1/exceptions/{exId}/severity",
            new { severity = "high", reason = "Escalated after manager review of the impact.", version = stale }));

        var conflict = await admin.PatchAsJsonAsync($"/api/v1/exceptions/{exId}/severity",
            new { severity = "critical", reason = "Escalated again using a stale version token.", version = stale });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
    }
}
