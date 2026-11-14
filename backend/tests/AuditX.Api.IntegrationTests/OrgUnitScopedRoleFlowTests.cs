using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AuditX.Api.IntegrationTests;

/// <summary>
/// Org-unit scoped roles: a role granted for an org unit, whose permissions are set to "assigned org unit", reaches only
/// the audits, auditable entities and findings that belong to that unit or a unit below it — in lists, on direct
/// reads and on writes. Its "whole organisation" permissions still apply everywhere.
/// </summary>
public sealed class OrgUnitScopedRoleFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
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
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.GetProperty("data").Clone();
    }

    private static async Task<Dictionary<string, Guid>> UsersByEmailAsync(HttpClient admin)
        => (await DataAsync(await admin.GetAsync("/api/v1/users?limit=200"))).GetProperty("items").EnumerateArray()
            .ToDictionary(u => u.GetProperty("email").GetString()!, u => u.GetProperty("id").GetGuid());

    private static async Task<Guid> OrgUnitAsync(HttpClient admin, string name, Guid? parent)
        => (await DataAsync(await admin.PostAsJsonAsync("/api/v1/org-units", new
        {
            name = $"{name} {Guid.NewGuid():N}"[..24],
            code = $"T{Random.Shared.Next(1_000_000, 9_999_999)}",
            parentOrgUnitId = parent,
        }))).GetProperty("id").GetGuid();

    private static async Task<Guid> EntityAsync(HttpClient client, string name, Guid? orgUnitId)
        => (await DataAsync(await client.PostAsJsonAsync("/api/v1/audit-universe/entities", new
        {
            name = $"{name} {Guid.NewGuid():N}",
            entityType = "process",
            orgUnitId,
        }))).GetProperty("id").GetGuid();

    private sealed record World(Guid UnitA, Guid UnitA1, Guid UnitB, Guid EntityA1, Guid EntityB, Guid EntityNone, Guid AuditA1, Guid AuditB);

    /// <summary>
    /// Org units A (with A1 below it) and B; an entity in A1, one in B and one in no unit; an audit of each of the first
    /// two. The auditor holds a custom role granted for A whose audit/universe permissions are "assigned org unit".
    /// </summary>
    private async Task<World> BuildAsync(HttpClient admin)
    {
        var users = await UsersByEmailAsync(admin);
        var unitA = await OrgUnitAsync(admin, "Unit A", null);
        var unitA1 = await OrgUnitAsync(admin, "Unit A1", unitA);
        var unitB = await OrgUnitAsync(admin, "Unit B", null);
        var entityA1 = await EntityAsync(admin, "Entity A1", unitA1);
        var entityB = await EntityAsync(admin, "Entity B", unitB);
        var entityNone = await EntityAsync(admin, "Entity none", null);

        async Task<Guid> AuditAsync(Guid entityId) => (await DataAsync(await admin.PostAsJsonAsync("/api/v1/audits", new
        {
            name = $"Scoped {Guid.NewGuid():N}",
            auditType = "operational",
            startDate = "2027-03-01",
            targetEndDate = "2027-04-01",
            entityId,
            leadUserId = users["admin@auditx.local"],
            auditeeUserId = users["auditee@auditx.local"],
        }))).GetProperty("id").GetGuid();

        var auditA1 = await AuditAsync(entityA1);
        var auditB = await AuditAsync(entityB);

        string[] limited =
        [
            "ViewAudits", "ViewAudit", "CreateAudit", "ManageAudit", "ViewUniverse", "ManageUniverse", "ViewExceptions",
        ];
        // Role changes go through maker-checker: the manager, made an administrator, approves the admin's new role.
        var roleName = $"Area auditor {Guid.NewGuid():N}"[..30];
        var queued = await admin.PostAsJsonAsync("/api/v1/roles", new
        {
            name = roleName,
            description = "Audits one area of the organisation.",
            permissions = limited.Select(k => new { key = k, scopeType = "org_unit", scopePredicateJson = (string?)null })
                .Append(new { key = "ViewTemplates", scopeType = "global", scopePredicateJson = (string?)null })
                .ToArray(),
            parentRoleIds = Array.Empty<Guid>(),
        });
        Assert.Equal(HttpStatusCode.Accepted, queued.StatusCode);
        var pendingId = JsonDocument.Parse(await queued.Content.ReadAsStringAsync()).RootElement
            .GetProperty("data").GetProperty("pendingActionId").GetGuid();

        var builtIn = (await DataAsync(await admin.GetAsync("/api/v1/roles"))).EnumerateArray()
            .ToDictionary(r => r.GetProperty("name").GetString()!, r => r.GetProperty("id").GetGuid());
        await admin.PostAsJsonAsync($"/api/v1/users/{users["manager@auditx.local"]}/roles",
            new { roleId = builtIn["AuditX Administrator"], scopeValue = (string?)null }); // idempotent across tests
        var checker = await LoginAsync("manager");
        (await checker.PostAsync($"/api/v1/maker-checker/{pendingId}/approve", null)).EnsureSuccessStatusCode();

        var role = (await DataAsync(await admin.GetAsync("/api/v1/roles"))).EnumerateArray()
            .Single(r => r.GetProperty("name").GetString() == roleName);
        Assert.Contains(role.GetProperty("permissions").EnumerateArray(),
            p => p.GetProperty("key").GetString() == "ManageAudit" && p.GetProperty("scopeType").GetString() == "org_unit");

        await DataAsync(await admin.PostAsJsonAsync($"/api/v1/users/{users["auditor@auditx.local"]}/roles",
            new { roleId = role.GetProperty("id").GetGuid(), scopeValue = unitA.ToString() }));

        return new World(unitA, unitA1, unitB, entityA1, entityB, entityNone, auditA1, auditB);
    }

    [Fact]
    public async Task A_role_granted_for_an_org_unit_reaches_only_what_belongs_there_and_below()
    {
        var admin = await LoginAsync("admin");
        var w = await BuildAsync(admin);
        var auditor = await LoginAsync("auditor");

        // Lists: the audit and entity in A1 (below A) — not B's, not the unassigned entity.
        var audits = (await DataAsync(await auditor.GetAsync("/api/v1/audits?pageSize=0"))).GetProperty("items")
            .EnumerateArray().Select(a => a.GetProperty("id").GetGuid()).ToArray();
        Assert.Contains(w.AuditA1, audits);
        Assert.DoesNotContain(w.AuditB, audits);

        var entities = (await DataAsync(await auditor.GetAsync("/api/v1/audit-universe/entities?pageSize=0"))).GetProperty("items")
            .EnumerateArray().Select(e => e.GetProperty("id").GetGuid()).ToArray();
        Assert.Contains(w.EntityA1, entities);
        Assert.DoesNotContain(w.EntityB, entities);
        Assert.DoesNotContain(w.EntityNone, entities);

        // The status counts, the findings tracker and global search apply the same narrowing.
        var counts = await DataAsync(await auditor.GetAsync("/api/v1/audits/counts"));
        Assert.Equal(1, counts.GetProperty("byStatus").GetProperty("draft").GetInt32());
        (await auditor.GetAsync("/api/v1/exceptions")).EnsureSuccessStatusCode();
        var hits = (await DataAsync(await auditor.GetAsync("/api/v1/search?q=Scoped"))).GetProperty("hits").EnumerateArray()
            .Where(h => h.GetProperty("type").GetString() == "audit").Select(h => h.GetProperty("id").GetString()).ToArray();
        Assert.Contains(w.AuditA1.ToString(), hits);
        Assert.DoesNotContain(w.AuditB.ToString(), hits);

        // Direct reads follow the same reach.
        Assert.Equal(HttpStatusCode.OK, (await auditor.GetAsync($"/api/v1/audits/{w.AuditA1}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await auditor.GetAsync($"/api/v1/audits/{w.AuditB}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await auditor.GetAsync($"/api/v1/audit-universe/entities/{w.EntityA1}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await auditor.GetAsync($"/api/v1/audit-universe/entities/{w.EntityB}")).StatusCode);

        // Writes: an entity can be placed in A1, but not in B and not outside every unit.
        await EntityAsync(auditor, "Auditor's own", w.UnitA1);
        Assert.Equal(HttpStatusCode.Forbidden, (await auditor.PostAsJsonAsync("/api/v1/audit-universe/entities",
            new { name = $"Elsewhere {Guid.NewGuid():N}", entityType = "process", orgUnitId = w.UnitB })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await auditor.PostAsJsonAsync("/api/v1/audit-universe/entities",
            new { name = $"Nowhere {Guid.NewGuid():N}", entityType = "process", orgUnitId = (Guid?)null })).StatusCode);

        // Its whole-organisation permission still applies everywhere.
        Assert.Equal(HttpStatusCode.OK, (await auditor.GetAsync("/api/v1/templates")).StatusCode);
    }

    [Fact]
    public async Task Only_permissions_on_things_that_belong_to_an_org_unit_can_be_limited_to_one()
    {
        var admin = await LoginAsync("admin");

        var catalogue = (await DataAsync(await admin.GetAsync("/api/v1/permissions/catalogue"))).EnumerateArray().ToArray();
        Assert.True(catalogue.Single(p => p.GetProperty("key").GetString() == "ManageAudit").GetProperty("orgUnitScopable").GetBoolean());
        Assert.False(catalogue.Single(p => p.GetProperty("key").GetString() == "ManageUsers").GetProperty("orgUnitScopable").GetBoolean());

        var response = await admin.PostAsJsonAsync("/api/v1/roles", new
        {
            name = $"Bad scope {Guid.NewGuid():N}"[..30],
            description = "Tries to limit user management to an org unit.",
            permissions = new[] { new { key = "ManageUsers", scopeType = "org_unit", scopePredicateJson = (string?)null } },
            parentRoleIds = Array.Empty<Guid>(),
        });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task A_role_whose_permissions_are_all_organisation_wide_cannot_be_limited_to_an_org_unit()
    {
        var admin = await LoginAsync("admin");
        var users = await UsersByEmailAsync(admin);
        var unit = await OrgUnitAsync(admin, "Unit X", null);
        var roles = (await DataAsync(await admin.GetAsync("/api/v1/roles"))).EnumerateArray()
            .ToDictionary(r => r.GetProperty("name").GetString()!, r => r.GetProperty("id").GetGuid());

        var limited = await admin.PostAsJsonAsync($"/api/v1/users/{users["manager@auditx.local"]}/roles",
            new { roleId = roles["Auditor"], scopeValue = unit.ToString() });
        Assert.Equal(HttpStatusCode.Conflict, limited.StatusCode);

        var notAUnit = await admin.PostAsJsonAsync($"/api/v1/users/{users["manager@auditx.local"]}/roles",
            new { roleId = roles["Auditor"], scopeValue = "BU-RETAIL" });
        Assert.Equal(HttpStatusCode.UnprocessableContent, notAUnit.StatusCode);
    }
}
