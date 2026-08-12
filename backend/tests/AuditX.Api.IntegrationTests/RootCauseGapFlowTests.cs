using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AuditX.Api.IntegrationTests;

/// <summary>
/// P2 root-cause gaps: the open→closed lifecycle, linking/unlinking contributing findings, the
/// closure-rationale guard, and the closed-gap edit lock.
/// </summary>
public sealed class RootCauseGapFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private HttpClient NewClient() => factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = true });

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

    private static async Task<Dictionary<string, Guid>> UsersByEmailAsync(HttpClient admin)
    {
        var data = await DataAsync(await admin.GetAsync("/api/v1/users?limit=100"));
        return data.GetProperty("items").EnumerateArray().ToDictionary(u => u.GetProperty("email").GetString()!, u => u.GetProperty("id").GetGuid());
    }

    private async Task<string> AuditVersionAsync(HttpClient admin, Guid auditId)
        => Version(await DataAsync(await admin.GetAsync($"/api/v1/audits/{auditId}")));

    /// <summary>Seed an in-progress audit with a failed item and an OPEN finding on it; return the finding id.</summary>
    private async Task<Guid> SeedFindingAsync(HttpClient admin)
    {
        var users = await UsersByEmailAsync(admin);
        var created = await DataAsync(await admin.PostAsJsonAsync("/api/v1/audits", new
        {
            name = $"RCG {Guid.NewGuid():N}", auditType = "branch", startDate = "2027-01-10", targetEndDate = "2027-02-10",
            leadUserId = users["manager@auditx.local"], auditeeUserId = users["auditee@auditx.local"],
        }));
        var auditId = created.GetProperty("id").GetGuid();
        var version = Version(created);
        var withItem = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/checklist/items", new { prompt = "Q", responseType = "pass_fail_na", isRequired = true, version }));
        version = Version(withItem);
        var withTeam = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/team", new { userId = users["auditor@auditx.local"], teamRole = "auditor", version }));
        version = Version(withTeam);
        var planned = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition", new { targetState = "planned", reason = (string?)null, version }));
        version = Version(planned);
        var started = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition", new { targetState = "in_progress", reason = (string?)null, version }));
        var itemId = started.GetProperty("checklistItems")[0].GetProperty("id").GetGuid();

        await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/items/{itemId}/responses",
            new { verdict = "fail", comment = "control missing", isDraft = false, version = await AuditVersionAsync(admin, auditId) }));
        var raised = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/exceptions", new
        {
            checklistItemId = itemId, title = "Gap contributor", severity = "high", rootCause = "x", recommendation = "y", ownerUserId = users["auditee@auditx.local"],
        }));
        return raised.GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Open_link_close_and_reopen_lifecycle()
    {
        var admin = await AdminAsync();
        var users = await UsersByEmailAsync(admin);
        var findingId = await SeedFindingAsync(admin);

        // Open a gap.
        var gap = await DataAsync(await admin.PostAsJsonAsync("/api/v1/root-cause-gaps", new
        {
            title = "No segregation-of-duties policy", description = "Recurring across branches", category = "process_gap",
            ownerUserId = users["manager@auditx.local"], targetDate = "2027-06-30",
        }));
        var gapId = gap.GetProperty("id").GetGuid();
        Assert.Equal("open", gap.GetProperty("status").GetString());

        // Link the finding.
        var linked = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/root-cause-gaps/{gapId}/exceptions",
            new { exceptionId = findingId, version = Version(gap) }));
        var linkedList = linked.GetProperty("linkedExceptions").EnumerateArray().ToArray();
        Assert.Contains(linkedList, e => e.GetProperty("exceptionId").GetGuid() == findingId);

        // It appears in the register with a linked-exception count.
        var list = await DataAsync(await admin.GetAsync("/api/v1/root-cause-gaps?status=open"));
        var row = list.GetProperty("items").EnumerateArray().First(g => g.GetProperty("id").GetGuid() == gapId);
        Assert.Equal(1, row.GetProperty("linkedExceptionCount").GetInt32());

        // Close it (rationale required).
        var closed = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/root-cause-gaps/{gapId}/close",
            new { rationale = "Policy introduced and rolled out group-wide.", version = Version(linked) }));
        Assert.Equal("closed", closed.GetProperty("status").GetString());

        // A closed gap rejects edits until reopened.
        var editWhileClosed = await admin.PatchAsJsonAsync($"/api/v1/root-cause-gaps/{gapId}", new
        {
            title = "Renamed", description = (string?)null, category = (string?)null,
            ownerUserId = users["manager@auditx.local"], targetDate = (string?)null, version = Version(closed),
        });
        Assert.Equal(HttpStatusCode.Conflict, editWhileClosed.StatusCode);

        // Reopen, then edit succeeds.
        var reopened = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/root-cause-gaps/{gapId}/reopen", new { version = Version(closed) }));
        Assert.Equal("open", reopened.GetProperty("status").GetString());
        var renamed = await DataAsync(await admin.PatchAsJsonAsync($"/api/v1/root-cause-gaps/{gapId}", new
        {
            title = "Renamed gap", description = (string?)null, category = (string?)null,
            ownerUserId = users["manager@auditx.local"], targetDate = (string?)null, version = Version(reopened),
        }));
        Assert.Equal("Renamed gap", renamed.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Closing_without_a_rationale_is_rejected()
    {
        var admin = await AdminAsync();
        var users = await UsersByEmailAsync(admin);
        var gap = await DataAsync(await admin.PostAsJsonAsync("/api/v1/root-cause-gaps", new
        {
            title = "Weak change management", description = (string?)null, category = (string?)null,
            ownerUserId = users["manager@auditx.local"], targetDate = (string?)null,
        }));
        var gapId = gap.GetProperty("id").GetGuid();

        // Too-short rationale is rejected by the domain guard.
        var response = await admin.PostAsJsonAsync($"/api/v1/root-cause-gaps/{gapId}/close", new { rationale = "too short", version = Version(gap) });
        Assert.Equal(HttpStatusCode.UnprocessableContent, response.StatusCode);
    }
}
