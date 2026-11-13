using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace AuditX.Api.IntegrationTests;

/// <summary>
/// Who may work a checklist item. Two rules, with no bypass for a manager or an administrator:
/// an ASSIGNED item is answered only by its assignee, and only members of the AUDIT'S TEAM can work its items at all
/// (respond, save or discard a draft, attach evidence).
/// </summary>
public sealed class ChecklistRespondRulesFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
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

    private static async Task<string> VersionAsync(HttpClient client, Guid auditId)
        => Version(await DataAsync(await client.GetAsync($"/api/v1/audits/{auditId}")));

    private static async Task<string> DetailAsync(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("detail").GetString() ?? string.Empty;

    private static MultipartFormDataContent PdfForm()
    {
        var form = new MultipartFormDataContent();
        var file = new ByteArrayContent(Encoding.ASCII.GetBytes("%PDF-1.4\n%AuditX evidence sample\n"));
        file.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(file, "file", "evidence.pdf");
        return form;
    }

    private sealed record Fixture(Guid AuditId, Guid AssignedItem, Guid OpenItem, Guid AuditorId, Guid ManagerId);

    /// <summary>
    /// An in-progress audit led by the manager, with the auditor on the team; one item ASSIGNED to the auditor and
    /// one left unassigned. The manager and auditor hold their built-in roles; the administrator (who sets it up)
    /// holds every permission but is deliberately NOT on the audit's team.
    /// </summary>
    private async Task<Fixture> StartAuditAsync(HttpClient admin)
    {
        var usersData = await DataAsync(await admin.GetAsync("/api/v1/users?limit=100"));
        var users = usersData.GetProperty("items").EnumerateArray()
            .ToDictionary(u => u.GetProperty("email").GetString()!, u => u.GetProperty("id").GetGuid());
        var roles = (await DataAsync(await admin.GetAsync("/api/v1/roles"))).EnumerateArray()
            .ToDictionary(r => r.GetProperty("name").GetString()!, r => r.GetProperty("id").GetGuid());

        // Idempotent across the class's shared database.
        await admin.PostAsJsonAsync($"/api/v1/users/{users["manager@auditx.local"]}/roles", new { roleId = roles["Audit Manager"], scopeValue = (string?)null });
        await admin.PostAsJsonAsync($"/api/v1/users/{users["auditor@auditx.local"]}/roles", new { roleId = roles["Auditor"], scopeValue = (string?)null });

        var auditorId = users["auditor@auditx.local"];
        var managerId = users["manager@auditx.local"];

        var created = await DataAsync(await admin.PostAsJsonAsync("/api/v1/audits", new
        {
            name = $"Respond rules {Guid.NewGuid():N}",
            auditType = "branch",
            startDate = "2027-01-10",
            targetEndDate = "2027-02-10",
            leadUserId = managerId,
            auditeeUserId = users["auditee@auditx.local"],
        }));
        var auditId = created.GetProperty("id").GetGuid();
        var version = Version(created);

        // The auditor must be on the team before an item can be assigned to them.
        var withTeam = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/team",
            new { userId = auditorId, teamRole = "auditor", version }));
        version = Version(withTeam);
        var a1 = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/checklist/items",
            new { prompt = "Assigned to the auditor", responseType = "pass_fail_na", isRequired = true, assignedUserId = auditorId, version }));
        version = Version(a1);
        var a2 = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/checklist/items",
            new { prompt = "Assigned to nobody", responseType = "pass_fail_na", isRequired = true, version }));
        version = Version(a2);
        var planned = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition", new { targetState = "planned", reason = (string?)null, version }));
        version = Version(planned);
        var started = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition", new { targetState = "in_progress", reason = (string?)null, version }));

        var items = started.GetProperty("checklistItems").EnumerateArray().ToArray();
        var assigned = items.First(i => i.GetProperty("assignedUserId").ValueKind != JsonValueKind.Null).GetProperty("id").GetGuid();
        var open = items.First(i => i.GetProperty("assignedUserId").ValueKind == JsonValueKind.Null).GetProperty("id").GetGuid();
        return new Fixture(auditId, assigned, open, auditorId, managerId);
    }

    private static Task<HttpResponseMessage> RespondAsync(HttpClient client, Fixture f, Guid itemId, string version, bool isDraft = false)
        => client.PostAsJsonAsync($"/api/v1/audits/{f.AuditId}/items/{itemId}/responses",
            new { verdict = isDraft ? null : "pass", comment = isDraft ? "working on it" : null, isDraft, version });

    [Fact]
    public async Task An_assigned_item_is_answered_only_by_its_assignee()
    {
        var admin = await LoginAsync("admin");
        var f = await StartAuditAsync(admin);
        var manager = await LoginAsync("manager");
        var auditor = await LoginAsync("auditor");

        // The manager LEADS this audit — on the team, holding ManageAudit — and still cannot answer the auditor's item.
        var refused = await RespondAsync(manager, f, f.AssignedItem, await VersionAsync(manager, f.AuditId));
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Contains("assigned to another team member", await DetailAsync(refused));

        // Not even as a draft.
        var refusedDraft = await RespondAsync(manager, f, f.AssignedItem, await VersionAsync(manager, f.AuditId), isDraft: true);
        Assert.Equal(HttpStatusCode.Forbidden, refusedDraft.StatusCode);

        // Nothing was recorded against the item.
        var none = await manager.GetAsync($"/api/v1/audits/{f.AuditId}/items/{f.AssignedItem}/responses");
        Assert.Equal(JsonValueKind.Null, JsonDocument.Parse(await none.Content.ReadAsStringAsync()).RootElement.GetProperty("data").ValueKind);

        // The assignee answers it.
        var answered = await DataAsync(await RespondAsync(auditor, f, f.AssignedItem, await VersionAsync(auditor, f.AuditId)));
        Assert.Equal(f.AuditorId, answered.GetProperty("responderUserId").GetGuid());

        // It is an ordinary response in the trail — there is no "override" path any more.
        var history = await DataAsync(await auditor.GetAsync($"/api/v1/audits/{f.AuditId}/items/{f.AssignedItem}/responses/history"));
        Assert.All(history.EnumerateArray(), e => Assert.Equal("item_responded", e.GetProperty("eventType").GetString()));
    }

    [Fact]
    public async Task An_unassigned_item_can_be_answered_by_any_team_member()
    {
        var admin = await LoginAsync("admin");
        var f = await StartAuditAsync(admin);
        var manager = await LoginAsync("manager");

        var answered = await DataAsync(await RespondAsync(manager, f, f.OpenItem, await VersionAsync(manager, f.AuditId)));

        Assert.Equal(f.ManagerId, answered.GetProperty("responderUserId").GetGuid());
    }

    [Fact]
    public async Task Reassigning_an_item_hands_it_to_the_new_assignee_and_takes_it_from_the_old_one()
    {
        var admin = await LoginAsync("admin");
        var f = await StartAuditAsync(admin);
        var manager = await LoginAsync("manager");
        var auditor = await LoginAsync("auditor");

        // To take an item over, a manager reassigns it (a recorded act) rather than answering someone else's item.
        (await manager.PatchAsJsonAsync($"/api/v1/audits/{f.AuditId}/items/{f.AssignedItem}/assignment",
            new { assigneeUserId = f.ManagerId, version = await VersionAsync(manager, f.AuditId) })).EnsureSuccessStatusCode();

        var formerAssignee = await RespondAsync(auditor, f, f.AssignedItem, await VersionAsync(auditor, f.AuditId));
        Assert.Equal(HttpStatusCode.Forbidden, formerAssignee.StatusCode);

        var answered = await DataAsync(await RespondAsync(manager, f, f.AssignedItem, await VersionAsync(manager, f.AuditId)));
        Assert.Equal(f.ManagerId, answered.GetProperty("responderUserId").GetGuid());
    }

    [Fact]
    public async Task Only_the_audit_team_can_work_checklist_items_whatever_permissions_the_caller_holds()
    {
        var admin = await LoginAsync("admin"); // every permission in the catalogue — but not on this audit's team
        var f = await StartAuditAsync(admin);
        var auditor = await LoginAsync("auditor");

        // Respond (even to an UNASSIGNED item) → refused.
        var refused = await RespondAsync(admin, f, f.OpenItem, await VersionAsync(admin, f.AuditId));
        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Contains("team", await DetailAsync(refused));

        // A team member starts a draft on that item…
        var draft = await DataAsync(await RespondAsync(auditor, f, f.OpenItem, await VersionAsync(auditor, f.AuditId), isDraft: true));
        var responseId = draft.GetProperty("id").GetGuid();

        // …an outsider can neither attach evidence to it nor discard it.
        using (var form = PdfForm())
        {
            var upload = await admin.PostAsync($"/api/v1/audits/{f.AuditId}/responses/{responseId}/evidence", form);
            Assert.Equal(HttpStatusCode.Forbidden, upload.StatusCode);
        }

        var discard = await admin.DeleteAsync(
            $"/api/v1/audits/{f.AuditId}/items/{f.OpenItem}/responses/draft?version={Uri.EscapeDataString(await VersionAsync(admin, f.AuditId))}");
        Assert.Equal(HttpStatusCode.Forbidden, discard.StatusCode);

        // The team member who is working the item can attach evidence to it.
        using (var form = PdfForm())
        {
            (await auditor.PostAsync($"/api/v1/audits/{f.AuditId}/responses/{responseId}/evidence", form)).EnsureSuccessStatusCode();
        }

        // The administrator still administers the audit (that is not checklist work): reading it is unaffected.
        (await admin.GetAsync($"/api/v1/audits/{f.AuditId}/checklist/progress")).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Evidence_on_an_assigned_item_is_attached_only_by_its_assignee()
    {
        var admin = await LoginAsync("admin");
        var f = await StartAuditAsync(admin);
        var manager = await LoginAsync("manager");
        var auditor = await LoginAsync("auditor");

        var draft = await DataAsync(await RespondAsync(auditor, f, f.AssignedItem, await VersionAsync(auditor, f.AuditId), isDraft: true));
        var responseId = draft.GetProperty("id").GetGuid();

        // Another team member — even the lead — cannot add evidence to the assignee's answer.
        using (var form = PdfForm())
        {
            var refused = await manager.PostAsync($"/api/v1/audits/{f.AuditId}/responses/{responseId}/evidence", form);
            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        }

        using (var form = PdfForm())
        {
            (await auditor.PostAsync($"/api/v1/audits/{f.AuditId}/responses/{responseId}/evidence", form)).EnsureSuccessStatusCode();
        }
    }

    [Fact]
    public async Task A_manager_on_the_team_may_clear_a_draft_left_behind()
    {
        var admin = await LoginAsync("admin");
        var f = await StartAuditAsync(admin);
        var manager = await LoginAsync("manager");
        var auditor = await LoginAsync("auditor");

        await DataAsync(await RespondAsync(auditor, f, f.OpenItem, await VersionAsync(auditor, f.AuditId), isDraft: true));

        // Clearing an abandoned draft is housekeeping, open to the audit's own manager — it answers nothing.
        var discard = await manager.DeleteAsync(
            $"/api/v1/audits/{f.AuditId}/items/{f.OpenItem}/responses/draft?version={Uri.EscapeDataString(await VersionAsync(manager, f.AuditId))}");
        Assert.Equal(HttpStatusCode.NoContent, discard.StatusCode);
    }
}
