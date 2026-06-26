using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AuditX.Api.IntegrationTests;

public sealed class AuditFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
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

    private static string Version(JsonElement audit) => audit.GetProperty("version").GetString()!;

    private static async Task<Dictionary<string, Guid>> UsersByEmailAsync(HttpClient admin)
    {
        var data = await DataAsync(await admin.GetAsync("/api/v1/users?limit=100"));
        return data.GetProperty("items").EnumerateArray()
            .ToDictionary(u => u.GetProperty("email").GetString()!, u => u.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Blank_audit_lifecycle_create_plan_start_review()
    {
        var admin = await AdminAsync();
        var users = await UsersByEmailAsync(admin);
        var lead = users["manager@auditx.local"];
        var auditee = users["auditee@auditx.local"];
        var auditor = users["auditor@auditx.local"];

        var created = await DataAsync(await admin.PostAsJsonAsync("/api/v1/audits", new
        {
            name = $"Branch Op Audit {Guid.NewGuid():N}",
            auditType = "branch",
            startDate = "2027-01-10",
            targetEndDate = "2027-02-10",
            leadUserId = lead,
            auditeeUserId = auditee,
        }));
        var auditId = created.GetProperty("id").GetGuid();
        var version = Version(created);
        Assert.Equal("draft", created.GetProperty("status").GetString());
        Assert.Equal(2, created.GetProperty("teamMembers").GetArrayLength()); // lead + auditee

        // Planning requires a checklist item and an auditor → 422 (business rule) before they exist.
        var earlyPlan = await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition", new { targetState = "planned", reason = (string?)null, version });
        Assert.Equal(HttpStatusCode.UnprocessableContent, earlyPlan.StatusCode);

        // Each child mutation advances the rowversion (interceptor promotes the root); echo the latest.
        var afterItem = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/checklist/items",
            new { prompt = "Cash counted daily?", responseType = "pass_fail_na", isRequired = true, version }));
        version = Version(afterItem);

        var afterTeam = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/team",
            new { userId = auditor, teamRole = "auditor", version }));
        version = Version(afterTeam);

        var planned = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition", new { targetState = "planned", reason = (string?)null, version }));
        Assert.Equal("planned", planned.GetProperty("status").GetString());
        version = Version(planned);

        var started = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition", new { targetState = "in_progress", reason = (string?)null, version }));
        Assert.Equal("in_progress", started.GetProperty("status").GetString());
        version = Version(started);

        // Moving to review with an unanswered item requires a reason.
        var noReason = await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition", new { targetState = "under_review", reason = (string?)null, version });
        Assert.Equal(HttpStatusCode.UnprocessableContent, noReason.StatusCode);

        var review = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition",
            new { targetState = "under_review", reason = "Submitting for review with one open item.", version }));
        Assert.Equal("under_review", review.GetProperty("status").GetString());
        version = Version(review);

        // Return-from-review edge (US-M4-016): under_review → in_progress requires a reason.
        var returned = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition",
            new { targetState = "in_progress", reason = "Reviewer asked for rework on the cash item.", version }));
        Assert.Equal("in_progress", returned.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Stale_version_is_rejected_with_conflict()
    {
        var admin = await AdminAsync();
        var users = await UsersByEmailAsync(admin);

        var created = await DataAsync(await admin.PostAsJsonAsync("/api/v1/audits", new
        {
            name = $"Concurrency {Guid.NewGuid():N}",
            auditType = "process",
            startDate = "2027-05-01",
            targetEndDate = "2027-05-31",
            leadUserId = users["manager@auditx.local"],
            auditeeUserId = users["auditee@auditx.local"],
        }));
        var auditId = created.GetProperty("id").GetGuid();
        var staleVersion = Version(created);

        // First edit succeeds and advances the version.
        await DataAsync(await admin.PatchAsJsonAsync($"/api/v1/audits/{auditId}",
            new { name = "Renamed once", scopeDescription = (string?)null, startDate = "2027-05-01", targetEndDate = "2027-05-31", version = staleVersion }));

        // Re-using the stale version must now 409.
        var conflict = await admin.PatchAsJsonAsync($"/api/v1/audits/{auditId}",
            new { name = "Renamed again", scopeDescription = (string?)null, startDate = "2027-05-01", targetEndDate = "2027-05-31", version = staleVersion });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
    }

    [Fact]
    public async Task Cancel_requires_a_substantial_reason()
    {
        var admin = await AdminAsync();
        var users = await UsersByEmailAsync(admin);

        var created = await DataAsync(await admin.PostAsJsonAsync("/api/v1/audits", new
        {
            name = $"Cancellable {Guid.NewGuid():N}",
            auditType = "process",
            startDate = "2027-03-01",
            targetEndDate = "2027-03-31",
            leadUserId = users["manager@auditx.local"],
            auditeeUserId = users["auditee@auditx.local"],
        }));
        var auditId = created.GetProperty("id").GetGuid();
        var version = Version(created);

        var tooShort = await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/cancel", new { reason = "nope", version });
        Assert.Equal(HttpStatusCode.UnprocessableContent, tooShort.StatusCode);

        var cancelled = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/cancel",
            new { reason = "Deprioritised for the current audit cycle.", version }));
        Assert.Equal("cancelled", cancelled.GetProperty("status").GetString());
    }
}
