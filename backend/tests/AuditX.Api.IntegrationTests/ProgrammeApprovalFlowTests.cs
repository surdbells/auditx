using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AuditX.Api.IntegrationTests;

/// <summary>
/// Scope, objectives and programme approval end to end, with the institution requiring approval before fieldwork (the
/// default): the lead prepares, another manager of the audit approves, and only then does fieldwork start.
/// </summary>
public sealed class ProgrammeApprovalFlowTests(StrictProgrammeApprovalApiFactory factory)
    : AccessModelTestBase(factory), IClassFixture<StrictProgrammeApprovalApiFactory>
{
    /// <summary>A planned audit led by <paramref name="leadEmail"/>, with the auditor on the team and admin on it too.</summary>
    private static async Task<JsonElement> PlannedAsync(HttpClient admin, Dictionary<string, Guid> users, string leadEmail)
    {
        var created = await DataAsync(await admin.PostAsJsonAsync("/api/v1/audits", new
        {
            name = $"Programme {Guid.NewGuid():N}", auditType = "branch", startDate = "2027-01-10", targetEndDate = "2027-02-10",
            scopeDescription = "Cash handling", leadUserId = users[leadEmail], auditeeUserId = users["auditee@auditx.local"],
        }));
        var auditId = created.GetProperty("id").GetGuid();
        var current = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/checklist/items",
            new { prompt = "Is dual control enforced?", responseType = "pass_fail_na", isRequired = true, version = Version(created) }));
        current = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/team",
            new { userId = users["auditor@auditx.local"], teamRole = "auditor", version = Version(current) }));
        if (leadEmail != "admin@auditx.local")
        {
            current = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/team",
                new { userId = users["admin@auditx.local"], teamRole = "reviewer", version = Version(current) }));
        }

        return await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition",
            new { targetState = "planned", reason = (string?)null, version = Version(current) }));
    }

    private static async Task<string[]> JourneyActionsAsync(HttpClient client, Guid auditId)
        => [.. (await DataAsync(await client.GetAsync($"/api/v1/engagements/{auditId}/journey")))
            .GetProperty("nextActions").EnumerateArray().Select(a => a.GetProperty("code").GetString()!)];

    [Fact]
    public async Task Fieldwork_waits_until_another_manager_approves_the_programme()
    {
        var admin = await LoginAsync("admin");
        var users = await UsersByEmailAsync(admin);
        var planned = await PlannedAsync(admin, users, "manager@auditx.local");
        var auditId = planned.GetProperty("id").GetGuid();

        // Fieldwork cannot start yet; the board offers the approval instead of the launch.
        var early = await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition",
            new { targetState = "in_progress", reason = (string?)null, version = Version(planned) });
        Assert.Equal(HttpStatusCode.UnprocessableContent, early.StatusCode);
        Assert.Contains("audit.programme_not_approved", await early.Content.ReadAsStringAsync());
        var actions = await JourneyActionsAsync(admin, auditId);
        Assert.Contains("approve_programme", actions);
        Assert.DoesNotContain("launch_fieldwork", actions);

        // The objectives are written before approval.
        var edited = await DataAsync(await admin.PatchAsJsonAsync($"/api/v1/audits/{auditId}", new
        {
            name = planned.GetProperty("name").GetString(), scopeDescription = "Cash handling",
            objectives = "Confirm that every payment over the limit has a second approver.",
            startDate = "2027-01-10", targetEndDate = "2027-02-10", version = Version(planned),
        }));
        Assert.Equal("Confirm that every payment over the limit has a second approver.", edited.GetProperty("objectives").GetString());

        // The auditee cannot touch how the audit is run.
        var auditee = await LoginAsync("auditee");
        var byAuditee = await auditee.PatchAsJsonAsync($"/api/v1/audits/{auditId}", new
        {
            name = "Renamed by the auditee", scopeDescription = "Nothing", startDate = "2027-01-10", targetEndDate = "2027-02-10", version = Version(edited),
        });
        Assert.Equal(HttpStatusCode.Forbidden, byAuditee.StatusCode);

        var approved = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/programme/approve",
            new { note = "Include the vault keys register.", version = Version(edited) }));
        Assert.Equal(users["admin@auditx.local"], approved.GetProperty("programmeApprovedBy").GetGuid());
        Assert.Equal("Include the vault keys register.", approved.GetProperty("programmeApprovalNote").GetString());
        Assert.Contains("launch_fieldwork", await JourneyActionsAsync(admin, auditId));

        // The approved scope is fixed while planned.
        var rescoped = await admin.PatchAsJsonAsync($"/api/v1/audits/{auditId}", new
        {
            name = planned.GetProperty("name").GetString(), scopeDescription = "Cash handling and loans",
            objectives = "Confirm that every payment over the limit has a second approver.",
            startDate = "2027-01-10", targetEndDate = "2027-02-10", version = Version(approved),
        });
        Assert.Equal(HttpStatusCode.Conflict, rescoped.StatusCode);

        var started = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition",
            new { targetState = "in_progress", reason = (string?)null, version = Version(approved) }));
        Assert.Equal("in_progress", started.GetProperty("status").GetString());
    }

    [Fact]
    public async Task The_lead_cannot_approve_their_own_programme()
    {
        var admin = await LoginAsync("admin");
        var users = await UsersByEmailAsync(admin);
        var planned = await PlannedAsync(admin, users, "admin@auditx.local");
        var auditId = planned.GetProperty("id").GetGuid();

        var own = await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/programme/approve", new { note = (string?)null, version = Version(planned) });
        Assert.Equal(HttpStatusCode.Forbidden, own.StatusCode);

        // The lead is not offered the approval either.
        Assert.DoesNotContain("approve_programme", await JourneyActionsAsync(admin, auditId));
    }
}
