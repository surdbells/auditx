using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AuditX.Api.IntegrationTests;

/// <summary>
/// M4 self-assessment mode: the area owner (auditee) runs an assessment of their own area — they are both lead
/// and auditee, no independent auditor is involved. Covers creation, the relaxed lifecycle (plan without an
/// auditor), self-assessor responses, and the guard that no one else may respond. The dev "auditee" arrives
/// without a role, so each test first grants them the Auditee built-in role (which carries RunSelfAssessment).
/// </summary>
public sealed class SelfAssessmentFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
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

    private static string Version(JsonElement audit) => audit.GetProperty("version").GetString()!;

    private async Task<string> VersionAsync(HttpClient client, Guid auditId)
        => Version(await DataAsync(await client.GetAsync($"/api/v1/audits/{auditId}")));

    private async Task<Guid> UserIdAsync(HttpClient admin, string email)
    {
        var users = await DataAsync(await admin.GetAsync("/api/v1/users?limit=100"));
        return users.GetProperty("items").EnumerateArray().First(u => u.GetProperty("email").GetString() == email).GetProperty("id").GetGuid();
    }

    private async Task<Guid> RoleIdAsync(HttpClient admin, string roleName)
    {
        var roles = await DataAsync(await admin.GetAsync("/api/v1/roles"));
        return roles.EnumerateArray().First(r => r.GetProperty("name").GetString() == roleName).GetProperty("id").GetGuid();
    }

    /// <summary>Grant the auditee dev user the Auditee built-in role (idempotent across the class DB).</summary>
    private async Task GrantAuditeeRoleAsync(HttpClient admin)
    {
        var roleId = await RoleIdAsync(admin, "Auditee");
        var userId = await UserIdAsync(admin, "auditee@auditx.local");
        await admin.PostAsJsonAsync($"/api/v1/users/{userId}/roles", new { roleId, scopeValue = (string?)null });
    }

    /// <summary>Author a template, publish it through maker-checker, and return its id (a checklist to assess against).</summary>
    private async Task<Guid> PublishTemplateAsync(HttpClient admin)
    {
        var created = await DataAsync(await admin.PostAsJsonAsync("/api/v1/templates", new
        {
            name = $"Self-assessment checklist {Guid.NewGuid():N}",
            auditType = "branch",
            description = "Branch self-review checklist",
        }));
        var templateId = created.GetProperty("id").GetGuid();

        await admin.PostAsJsonAsync($"/api/v1/templates/{templateId}/items", new
        {
            prompt = "Are opening balances reconciled?",
            responseType = "pass_fail_na",
            isRequired = true,
        });

        var publish = await admin.PostAsync($"/api/v1/templates/{templateId}/publish", null);
        var pendingId = (await DataAsync(publish)).GetProperty("pendingActionId").GetGuid();
        var manager = await LoginAsync("manager"); // a different user approves the maker-checker action
        (await manager.PostAsync($"/api/v1/maker-checker/{pendingId}/approve", null)).EnsureSuccessStatusCode();

        return templateId;
    }

    [Fact]
    public async Task Auditee_runs_a_full_self_assessment_from_a_template()
    {
        var admin = await LoginAsync("admin");
        var templateId = await PublishTemplateAsync(admin);
        await GrantAuditeeRoleAsync(admin);

        var auditee = await LoginAsync("auditee");

        // The area owner starts a self-assessment of their own area. They become both lead and auditee.
        var created = await DataAsync(await auditee.PostAsJsonAsync("/api/v1/audits/self-assessment", new
        {
            name = $"Self-assessment {Guid.NewGuid():N}",
            auditType = "branch",
            startDate = "2027-03-01",
            targetEndDate = "2027-03-31",
            scopeDescription = "Quarterly branch self-review",
            templateId,
        }));
        var auditId = created.GetProperty("id").GetGuid();
        Assert.True(created.GetProperty("isSelfAssessment").GetBoolean());
        Assert.Equal("draft", created.GetProperty("status").GetString());
        var lead = created.GetProperty("leadUserId").GetGuid();
        Assert.Equal(lead, created.GetProperty("auditeeUserId").GetGuid()); // same person
        Assert.Single(created.GetProperty("teamMembers").EnumerateArray()); // a single Lead member, no duplicate/auditor
        Assert.NotEmpty(created.GetProperty("checklistItems").EnumerateArray()); // template seeded the checklist

        // The assessor plans + starts their own assessment — no independent auditor required for a self-assessment.
        var version = Version(created);
        var planned = await DataAsync(await auditee.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition",
            new { targetState = "planned", reason = (string?)null, version }));
        Assert.Equal("planned", planned.GetProperty("status").GetString());
        version = Version(planned);
        var started = await DataAsync(await auditee.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition",
            new { targetState = "in_progress", reason = (string?)null, version }));
        Assert.Equal("in_progress", started.GetProperty("status").GetString());

        var items = started.GetProperty("checklistItems").EnumerateArray()
            .OrderBy(i => i.GetProperty("orderIndex").GetInt32()).ToArray();

        // The assessor responds to every item themselves; the final finalisation auto-transitions to Under Review.
        foreach (var item in items)
        {
            var itemId = item.GetProperty("id").GetGuid();
            await DataAsync(await auditee.PostAsJsonAsync($"/api/v1/audits/{auditId}/items/{itemId}/responses",
                new { verdict = "pass", comment = (string?)null, isDraft = false, version = await VersionAsync(auditee, auditId) }));
        }

        var after = await DataAsync(await auditee.GetAsync($"/api/v1/audits/{auditId}"));
        Assert.Equal("under_review", after.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Self_assessment_requires_a_template()
    {
        var admin = await LoginAsync("admin");
        await GrantAuditeeRoleAsync(admin);
        var auditee = await LoginAsync("auditee");

        var resp = await auditee.PostAsJsonAsync("/api/v1/audits/self-assessment", new
        {
            name = $"Empty self-assessment {Guid.NewGuid():N}",
            auditType = "branch",
            startDate = "2027-03-01",
            targetEndDate = "2027-03-31",
            scopeDescription = "No template",
            templateId = (Guid?)null,
        });
        Assert.Equal(HttpStatusCode.UnprocessableContent, resp.StatusCode);
    }

    [Fact]
    public async Task Only_the_self_assessor_may_respond()
    {
        var admin = await LoginAsync("admin");
        var templateId = await PublishTemplateAsync(admin);
        await GrantAuditeeRoleAsync(admin);
        var auditee = await LoginAsync("auditee");

        var created = await DataAsync(await auditee.PostAsJsonAsync("/api/v1/audits/self-assessment", new
        {
            name = $"Self-assessment {Guid.NewGuid():N}",
            auditType = "branch",
            startDate = "2027-03-01",
            targetEndDate = "2027-03-31",
            scopeDescription = "Owner-only response",
            templateId,
        }));
        var auditId = created.GetProperty("id").GetGuid();
        var version = Version(created);
        var planned = await DataAsync(await auditee.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition",
            new { targetState = "planned", reason = (string?)null, version }));
        version = Version(planned);
        var started = await DataAsync(await auditee.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition",
            new { targetState = "in_progress", reason = (string?)null, version }));
        var itemId = started.GetProperty("checklistItems").EnumerateArray().First().GetProperty("id").GetGuid();
        // A fresh version fetched by the assessor, so authorisation — not a stale rowversion — is what decides.
        var currentVersion = await VersionAsync(auditee, auditId);

        // An auditor — who could respond on a normal audit — is forbidden on someone else's self-assessment.
        var auditor = await LoginAsync("auditor");
        var forbidden = await auditor.PostAsJsonAsync($"/api/v1/audits/{auditId}/items/{itemId}/responses",
            new { verdict = "pass", comment = (string?)null, isDraft = false, version = currentVersion });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        // Even a manager (who holds ManageAudit) cannot respond to another person's self-assessment.
        var manager = await LoginAsync("manager");
        var managerForbidden = await manager.PostAsJsonAsync($"/api/v1/audits/{auditId}/items/{itemId}/responses",
            new { verdict = "pass", comment = (string?)null, isDraft = false, version = currentVersion });
        Assert.Equal(HttpStatusCode.Forbidden, managerForbidden.StatusCode);
    }
}
