using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AuditX.Api.IntegrationTests;

/// <summary>
/// An audit counts toward an entity's coverage through the entity it is linked to — whether it was launched from the
/// annual plan or created ad hoc. These tests pin the ad-hoc path end to end: choosing the entity at creation, linking
/// an already-completed audit afterwards, the coverage matrix / "last audited" / not-audited list reacting to it, the
/// entity's full audit history, and the bulk importer the universe is loaded with.
/// </summary>
public sealed class EntityCoverageFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private async Task<HttpClient> AdminAsync()
    {
        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { HandleCookies = true });
        (await client.PostAsJsonAsync("/api/v1/auth/login", new { username = "admin", password = "Passw0rd!" })).EnsureSuccessStatusCode();
        return client;
    }

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode} from {response.RequestMessage?.RequestUri?.PathAndQuery}: {body}");
        return JsonDocument.Parse(body).RootElement.GetProperty("data").Clone();
    }

    private static string Version(JsonElement node) => node.GetProperty("version").GetString()!;

    private static async Task<Dictionary<string, Guid>> UsersByEmailAsync(HttpClient admin)
    {
        var data = await DataAsync(await admin.GetAsync("/api/v1/users?limit=100"));
        return data.GetProperty("items").EnumerateArray()
            .ToDictionary(u => u.GetProperty("email").GetString()!, u => u.GetProperty("id").GetGuid());
    }

    private static async Task<JsonElement> CreateEntityAsync(HttpClient admin, string entityType = "branch")
        => await DataAsync(await admin.PostAsJsonAsync("/api/v1/audit-universe/entities",
            new { name = $"Coverage {Guid.NewGuid():N}", entityType }));

    private static async Task<JsonElement> EntityAsync(HttpClient admin, Guid id)
        => await DataAsync(await admin.GetAsync($"/api/v1/audit-universe/entities/{id}"));

    /// <summary>An ad-hoc (not plan-linked) audit with one item; admin is on its team so admin can answer it.</summary>
    private static async Task<(Guid AuditId, Guid ItemId)> CreateStartedAdHocAuditAsync(
        HttpClient admin, Dictionary<string, Guid> users, string auditType, Guid? entityId)
    {
        var created = await DataAsync(await admin.PostAsJsonAsync("/api/v1/audits", new
        {
            name = $"Ad hoc {Guid.NewGuid():N}", auditType, startDate = "2027-01-10", targetEndDate = "2027-02-10",
            leadUserId = users["manager@auditx.local"], auditeeUserId = users["auditee@auditx.local"], entityId,
        }));
        var auditId = created.GetProperty("id").GetGuid();
        var withItem = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/checklist/items",
            new { prompt = "Is dual control enforced?", responseType = "pass_fail_na", isRequired = true, version = Version(created) }));
        var withTeam = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/team",
            new { userId = users["admin@auditx.local"], teamRole = "auditor", version = Version(withItem) }));
        var planned = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition",
            new { targetState = "planned", reason = (string?)null, version = Version(withTeam) }));
        var started = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition",
            new { targetState = "in_progress", reason = (string?)null, version = Version(planned) }));
        return (auditId, started.GetProperty("checklistItems")[0].GetProperty("id").GetGuid());
    }

    private static async Task<string> AuditVersionAsync(HttpClient admin, Guid auditId)
        => Version(await DataAsync(await admin.GetAsync($"/api/v1/audits/{auditId}")));

    private static async Task CompleteAsync(HttpClient admin, Guid auditId, Guid itemId)
    {
        // Answering the last open item moves the audit to review on its own; completing it is the explicit step.
        await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/items/{itemId}/responses",
            new { verdict = "pass", comment = (string?)null, isDraft = false, version = await AuditVersionAsync(admin, auditId) }));
        var completed = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition",
            new { targetState = "completed", reason = (string?)null, version = await AuditVersionAsync(admin, auditId) }));
        Assert.Equal("completed", completed.GetProperty("status").GetString());
    }

    /// <summary>The coverage-matrix cell for (entity type, audit type); 0 when either is not on the grid yet.</summary>
    private static async Task<int> MatrixCellAsync(HttpClient admin, string entityType, string auditType)
    {
        var matrix = await DataAsync(await admin.GetAsync("/api/v1/coverage-analytics/matrix?window=12"));
        var rows = matrix.GetProperty("rows").EnumerateArray().Select(x => x.GetString()).ToList();
        var columns = matrix.GetProperty("columns").EnumerateArray().Select(x => x.GetString()).ToList();
        var r = rows.FindIndex(x => string.Equals(x, entityType, StringComparison.OrdinalIgnoreCase));
        var c = columns.FindIndex(x => string.Equals(x, auditType, StringComparison.OrdinalIgnoreCase));
        return r < 0 || c < 0 ? 0 : matrix.GetProperty("cells")[r][c].GetInt32();
    }

    [Fact]
    public async Task An_ad_hoc_audit_of_an_entity_counts_toward_its_coverage_and_shows_in_its_history()
    {
        var admin = await AdminAsync();
        var users = await UsersByEmailAsync(admin);
        var entity = await CreateEntityAsync(admin);
        var entityId = entity.GetProperty("id").GetGuid();
        // An audit type no template or plan item uses — the matrix must still give it a column.
        const string auditType = "adhoc_special_review";

        // Never audited: listed as a coverage gap, nothing on the matrix.
        var gapsBefore = await DataAsync(await admin.GetAsync("/api/v1/coverage-analytics/not-audited-since?months=12"));
        Assert.Contains(gapsBefore.EnumerateArray(), e => e.GetProperty("id").GetGuid() == entityId);
        var cellBefore = await MatrixCellAsync(admin, "branch", auditType);

        // An ad-hoc audit created WITH the entity keeps the link…
        var (auditId, itemId) = await CreateStartedAdHocAuditAsync(admin, users, auditType, entityId);
        var audit = await DataAsync(await admin.GetAsync($"/api/v1/audits/{auditId}"));
        Assert.Equal(entityId, audit.GetProperty("auditableEntityId").GetGuid());
        Assert.Equal(entity.GetProperty("name").GetString(), audit.GetProperty("auditableEntityName").GetString());
        Assert.True(audit.GetProperty("planItemId").ValueKind == JsonValueKind.Null);

        // …and an unknown entity is refused outright.
        var bogus = await admin.PostAsJsonAsync("/api/v1/audits", new
        {
            name = "Bogus", auditType, startDate = "2027-01-10", targetEndDate = "2027-02-10",
            leadUserId = users["manager@auditx.local"], auditeeUserId = users["auditee@auditx.local"], entityId = Guid.NewGuid(),
        });
        Assert.Equal(HttpStatusCode.NotFound, bogus.StatusCode);

        // In flight it does not count yet.
        Assert.True((await EntityAsync(admin, entityId)).GetProperty("lastAuditedAt").ValueKind == JsonValueKind.Null);

        await CompleteAsync(admin, auditId, itemId);

        // Completed: the entity is stamped, drops off the gap list, and the matrix counts the audit.
        Assert.True((await EntityAsync(admin, entityId)).GetProperty("lastAuditedAt").ValueKind != JsonValueKind.Null);
        var gapsAfter = await DataAsync(await admin.GetAsync("/api/v1/coverage-analytics/not-audited-since?months=12"));
        Assert.DoesNotContain(gapsAfter.EnumerateArray(), e => e.GetProperty("id").GetGuid() == entityId);
        Assert.Equal(cellBefore + 1, await MatrixCellAsync(admin, "branch", auditType));

        // A second audit of the same entity, not yet completed, with a failed item and a finding raised on it.
        var (secondId, secondItem) = await CreateStartedAdHocAuditAsync(admin, users, auditType, entityId);
        await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{secondId}/items/{secondItem}/responses",
            new { verdict = "fail", comment = "No maker-checker.", isDraft = false, version = await AuditVersionAsync(admin, secondId) }));
        await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{secondId}/exceptions", new
        {
            checklistItemId = secondItem, title = "Segregation gap", severity = "high",
            rootCause = "x", recommendation = "y", ownerUserId = users["auditee@auditx.local"],
        }));

        // The entity's audit history: both audits, newest first is not asserted (same start date) — content is.
        var history = await DataAsync(await admin.GetAsync($"/api/v1/audit-universe/entities/{entityId}/audit-history"));
        Assert.Equal(2, history.GetProperty("totalAudits").GetInt32());
        Assert.Equal(1, history.GetProperty("completedAudits").GetInt32());
        Assert.Equal(1, history.GetProperty("inFlightAudits").GetInt32());
        Assert.Equal(1, history.GetProperty("totalFindings").GetInt32());
        Assert.Equal(1, history.GetProperty("openFindings").GetInt32());
        Assert.True(history.GetProperty("lastAuditedAt").ValueKind != JsonValueKind.Null);

        var rows = history.GetProperty("audits").EnumerateArray().ToDictionary(a => a.GetProperty("auditId").GetGuid());
        var first = rows[auditId];
        Assert.Equal("completed", first.GetProperty("status").GetString());
        Assert.Equal("ad_hoc", first.GetProperty("origin").GetString());
        Assert.Equal(1, first.GetProperty("passed").GetInt32());
        Assert.Equal(0, first.GetProperty("findings").GetInt32());
        Assert.False(string.IsNullOrWhiteSpace(first.GetProperty("leadName").GetString()));
        Assert.True(first.GetProperty("actualEndDate").ValueKind != JsonValueKind.Null);

        var second = rows[secondId];
        Assert.Equal("under_review", second.GetProperty("status").GetString());
        Assert.Equal(1, second.GetProperty("failed").GetInt32());
        Assert.Equal(1, second.GetProperty("findings").GetInt32());
        Assert.Equal(1, second.GetProperty("highFindings").GetInt32());
        Assert.Equal(1, second.GetProperty("openFindings").GetInt32());

        // Unknown entity → 404.
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/audit-universe/entities/{Guid.NewGuid()}/audit-history")).StatusCode);
    }

    [Fact]
    public async Task A_completed_ad_hoc_audit_can_be_linked_to_its_entity_afterwards_and_the_credit_follows_it()
    {
        var admin = await AdminAsync();
        var users = await UsersByEmailAsync(admin);
        var first = (await CreateEntityAsync(admin)).GetProperty("id").GetGuid();
        var second = (await CreateEntityAsync(admin, "process")).GetProperty("id").GetGuid();
        const string auditType = "adhoc_backfill_review";

        // An audit created WITHOUT an entity (how every ad-hoc audit used to be created), then completed.
        var (auditId, itemId) = await CreateStartedAdHocAuditAsync(admin, users, auditType, entityId: null);
        await CompleteAsync(admin, auditId, itemId);
        Assert.True((await EntityAsync(admin, first)).GetProperty("lastAuditedAt").ValueKind == JsonValueKind.Null);
        var branchCellBefore = await MatrixCellAsync(admin, "branch", auditType);

        // Link it to the entity it actually examined: that entity is now audited and the matrix counts it.
        var linked = await DataAsync(await admin.PutAsJsonAsync($"/api/v1/audits/{auditId}/entity",
            new { entityId = first, version = await AuditVersionAsync(admin, auditId) }));
        Assert.Equal(first, linked.GetProperty("auditableEntityId").GetGuid());
        Assert.True((await EntityAsync(admin, first)).GetProperty("lastAuditedAt").ValueKind != JsonValueKind.Null);
        Assert.Equal(branchCellBefore + 1, await MatrixCellAsync(admin, "branch", auditType));

        // Linked to the wrong one? Move it: the credit moves too — the first entity is "never audited" again.
        await DataAsync(await admin.PutAsJsonAsync($"/api/v1/audits/{auditId}/entity",
            new { entityId = second, version = Version(linked) }));
        Assert.True((await EntityAsync(admin, first)).GetProperty("lastAuditedAt").ValueKind == JsonValueKind.Null);
        Assert.True((await EntityAsync(admin, second)).GetProperty("lastAuditedAt").ValueKind != JsonValueKind.Null);
        Assert.Equal(branchCellBefore, await MatrixCellAsync(admin, "branch", auditType));

        // The link is on the audit's own history.
        var trail = await DataAsync(await admin.GetAsync($"/api/v1/audits/{auditId}/history"));
        Assert.Equal(2, trail.EnumerateArray().Count(e => e.GetProperty("eventType").GetString() == "audit_entity_linked"));

        // Unknown entity → 404; a stale version → 409.
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PutAsJsonAsync($"/api/v1/audits/{auditId}/entity",
            new { entityId = Guid.NewGuid(), version = await AuditVersionAsync(admin, auditId) })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PutAsJsonAsync($"/api/v1/audits/{auditId}/entity",
            new { entityId = first, version = Version(linked) })).StatusCode);
    }

    [Fact]
    public async Task The_entity_importer_accepts_a_spreadsheet_export_and_explains_a_wrong_header()
    {
        var admin = await AdminAsync();
        var parent = $"Region {Guid.NewGuid():N}";
        var child = $"Branch {Guid.NewGuid():N}";

        // As saved by Excel ("CSV UTF-8": leading byte-order mark, CRLF), with hand-typed header names, an optional
        // column left out, and the child listed before nothing but its in-sheet parent.
        var sheet = "﻿Name,Entity Type,Parent Name,Description\r\n"
            + $"{parent},branch,,Regional office\r\n"
            + $"{child},branch,{parent},\"Sub-branch, north\"\r\n";
        var imported = await DataAsync(await admin.PostAsJsonAsync("/api/v1/audit-universe/entities/bulk-import", new { csvContent = sheet }));
        Assert.Equal(0, imported.GetProperty("errors").GetArrayLength());
        Assert.Equal(2, imported.GetProperty("created").GetInt32());

        var found = await DataAsync(await admin.GetAsync($"/api/v1/audit-universe/entities?search={child}&pageSize=10"));
        var row = found.GetProperty("items").EnumerateArray().Single();
        Assert.Equal("Sub-branch, north", (await EntityAsync(admin, row.GetProperty("id").GetGuid())).GetProperty("description").GetString());

        // A sheet without the entity-type column is refused once, with the layout it should have — not per row.
        var wrong = await DataAsync(await admin.PostAsJsonAsync("/api/v1/audit-universe/entities/bulk-import",
            new { csvContent = "name,category\nSomething,branch\nSomething else,branch\n" }));
        Assert.Equal(0, wrong.GetProperty("created").GetInt32());
        var error = Assert.Single(wrong.GetProperty("errors").EnumerateArray());
        Assert.Equal(1, error.GetProperty("row").GetInt32());
        Assert.Contains("name,entity_type,parent_name,owner_email,description", error.GetProperty("message").GetString());

        // An unknown type names the valid ones; nothing from that sheet is created (atomic).
        var good = $"Good {Guid.NewGuid():N}";
        var mixed = await DataAsync(await admin.PostAsJsonAsync("/api/v1/audit-universe/entities/bulk-import",
            new { csvContent = $"name,entity_type\n{good},branch\nBad row,not_a_type\n" }));
        Assert.Equal(0, mixed.GetProperty("created").GetInt32());
        Assert.Contains("branch", Assert.Single(mixed.GetProperty("errors").EnumerateArray()).GetProperty("message").GetString());
        var none = await DataAsync(await admin.GetAsync($"/api/v1/audit-universe/entities?search={good}&pageSize=10"));
        Assert.Equal(0, none.GetProperty("items").GetArrayLength());
    }
}
