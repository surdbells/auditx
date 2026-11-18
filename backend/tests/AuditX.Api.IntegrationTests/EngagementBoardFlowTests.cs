using System.Net.Http.Json;
using System.Text.Json;

namespace AuditX.Api.IntegrationTests;

/// <summary>
/// GET engagements lists the caller's engagements: the audits they are on the team of, and those they manage. Each
/// journey reads the audit's team, checklist and independence declarations, so its next steps are the ones the audit
/// page offers. Runs with independence declarations required, the default policy.
/// </summary>
public sealed class EngagementBoardFlowTests(StrictIndependenceApiFactory factory) : IClassFixture<StrictIndependenceApiFactory>
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

    private static string Version(JsonElement audit) => audit.GetProperty("version").GetString()!;

    /// <summary>The caller's board: each listed audit with the codes of its next steps.</summary>
    private static async Task<Dictionary<Guid, string[]>> BoardAsync(HttpClient client, string? status = null)
    {
        var board = await DataAsync(await client.GetAsync(status is null ? "/api/v1/engagements" : $"/api/v1/engagements?status={status}"));
        return board.EnumerateArray().ToDictionary(
            i => i.GetProperty("auditId").GetGuid(),
            i => i.GetProperty("nextActions").EnumerateArray().Select(a => a.GetProperty("code").GetString()!).ToArray());
    }

    [Fact]
    public async Task Team_members_see_the_audits_they_work_on_with_the_steps_their_team_and_checklist_call_for()
    {
        var admin = await LoginAsync("admin");
        var users = (await DataAsync(await admin.GetAsync("/api/v1/users?limit=200"))).GetProperty("items").EnumerateArray()
            .ToDictionary(u => u.GetProperty("email").GetString()!, u => u.GetProperty("id").GetGuid());
        var auditorId = users["auditor@auditx.local"];

        async Task<JsonElement> CreateAsync(string name) => await DataAsync(await admin.PostAsJsonAsync("/api/v1/audits", new
        {
            name = $"{name} {Guid.NewGuid():N}", auditType = "operational", startDate = "2027-01-10", targetEndDate = "2027-02-10",
            leadUserId = users["manager@auditx.local"], auditeeUserId = users["auditee@auditx.local"],
        }));

        async Task<JsonElement> StaffAsync(JsonElement audit)
        {
            var id = audit.GetProperty("id").GetGuid();
            var withItem = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{id}/checklist/items",
                new { prompt = "Cash counted?", responseType = "pass_fail_na", isRequired = true, version = Version(audit) }));
            return await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{id}/team",
                new { userId = auditorId, teamRole = "auditor", version = Version(withItem) }));
        }

        // The auditor works on two audits; a third is staffed without them.
        var mine = await StaffAsync(await CreateAsync("Board mine"));
        var mineId = mine.GetProperty("id").GetGuid();
        var stayId = (await StaffAsync(await CreateAsync("Board stay"))).GetProperty("id").GetGuid();
        var otherId = (await CreateAsync("Board other")).GetProperty("id").GetGuid();

        // A plain team member (no role, no ManageAudit) sees the audits they are on, each asking for their independence
        // declaration, and not the audit they are not on.
        var auditor = await LoginAsync("auditor");
        var auditorBoard = await BoardAsync(auditor);
        Assert.Contains("declare_independence", auditorBoard[mineId]);
        Assert.Contains(stayId, auditorBoard.Keys);
        Assert.DoesNotContain(otherId, auditorBoard.Keys);

        // The lead sees every audit they lead, though their own role does not reach audits outside its org units.
        var leadBoard = await BoardAsync(await LoginAsync("manager"));
        Assert.Contains(mineId, leadBoard.Keys);
        Assert.Contains(otherId, leadBoard.Keys);

        // An administrator manages every audit. Their next step follows the audit's team and checklist: the staffed
        // audit is ready to plan, the unstaffed one needs setting up.
        var adminBoard = await BoardAsync(admin);
        Assert.Contains("plan_audit", adminBoard[mineId]);
        Assert.DoesNotContain("setup_engagement", adminBoard[mineId]);
        Assert.Contains("setup_engagement", adminBoard[otherId]);

        // Planned, fieldwork waits for the team's declarations: no launch is offered while they are outstanding. (Only
        // the team moves an audit through its lifecycle, so the administrator joins it to plan it.)
        mine = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{mineId}/team",
            new { userId = users["admin@auditx.local"], teamRole = "reviewer", version = Version(mine) }));
        mine = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{mineId}/transition",
            new { targetState = "planned", reason = (string?)null, version = Version(mine) }));
        Assert.DoesNotContain("launch_fieldwork", (await BoardAsync(admin))[mineId]);
        var planned = await BoardAsync(admin, "planned");
        Assert.Contains(mineId, planned.Keys);
        Assert.DoesNotContain(otherId, planned.Keys);

        // Once off the team, the auditor no longer finds the audit on their board.
        var membershipId = mine.GetProperty("teamMembers").EnumerateArray()
            .First(m => m.GetProperty("userId").GetGuid() == auditorId).GetProperty("id").GetGuid();
        var removed = await admin.DeleteAsync($"/api/v1/audits/{mineId}/team/{membershipId}?version={Uri.EscapeDataString(Version(mine))}");
        Assert.True(removed.IsSuccessStatusCode, await removed.Content.ReadAsStringAsync());
        auditorBoard = await BoardAsync(auditor);
        Assert.DoesNotContain(mineId, auditorBoard.Keys);
        Assert.Contains(stayId, auditorBoard.Keys);
    }
}
