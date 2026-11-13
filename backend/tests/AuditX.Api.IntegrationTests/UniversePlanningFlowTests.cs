using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AuditX.Api.IntegrationTests;

public sealed class UniversePlanningFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
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

    [Fact]
    public async Task Entity_create_score_and_coverage_flow()
    {
        var admin = await AdminAsync();

        var created = await DataAsync(await admin.PostAsJsonAsync("/api/v1/audit-universe/entities", new
        {
            name = $"Lagos Branch {Guid.NewGuid():N}",
            entityType = "branch",
            description = "Flagship branch",
        }));
        var entityId = created.GetProperty("id").GetGuid();
        Assert.Equal("branch", created.GetProperty("entityType").GetString());

        // Score every active seeded dimension (Financial/Operational/Regulatory/Reputational).
        var scores = new Dictionary<string, int> { ["Financial"] = 4, ["Operational"] = 3, ["Regulatory"] = 5, ["Reputational"] = 2 };
        var scored = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audit-universe/entities/{entityId}/risk-scores", new
        {
            inherentScores = scores,
            residualScores = scores,
            version = created.GetProperty("version").GetString(),
        }));
        Assert.True(scored.GetProperty("compositeResidualScore").GetDecimal() > 0);

        // Incomplete score set is rejected (422) — use a fresh entity to avoid version staleness.
        var other = await DataAsync(await admin.PostAsJsonAsync("/api/v1/audit-universe/entities", new { name = $"B{Guid.NewGuid():N}", entityType = "branch" }));
        var bad = await admin.PostAsJsonAsync($"/api/v1/audit-universe/entities/{other.GetProperty("id").GetGuid()}/risk-scores", new
        {
            inherentScores = new Dictionary<string, int> { ["Financial"] = 4 },
            version = other.GetProperty("version").GetString(),
        });
        Assert.Equal(HttpStatusCode.UnprocessableContent, bad.StatusCode);

        // Coverage: a never-audited entity appears in not-audited-since.
        var coverage = await DataAsync(await admin.GetAsync("/api/v1/coverage-analytics/not-audited-since?months=12"));
        Assert.Contains(coverage.EnumerateArray(), e => e.GetProperty("id").GetGuid() == entityId);
    }

    /// <summary>
    /// Archiving only hides an entity, so it must be possible to bring it back: an archived entity is restored with
    /// its details and risk scores intact, reappears in the live universe, and is editable again.
    /// </summary>
    [Fact]
    public async Task An_archived_entity_can_be_restored_with_everything_it_had()
    {
        var admin = await AdminAsync();
        var name = $"Restorable {Guid.NewGuid():N}";

        var created = await DataAsync(await admin.PostAsJsonAsync("/api/v1/audit-universe/entities", new
        {
            name, entityType = "branch", description = "Kept through archive",
        }));
        var id = created.GetProperty("id").GetGuid();
        var scores = new Dictionary<string, int> { ["Financial"] = 4, ["Operational"] = 3, ["Regulatory"] = 5, ["Reputational"] = 2 };
        var scored = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audit-universe/entities/{id}/risk-scores", new
        {
            inherentScores = scores, residualScores = scores, version = created.GetProperty("version").GetString(),
        }));
        var residual = scored.GetProperty("compositeResidualScore").GetDecimal();

        async Task<JsonElement[]> SearchAsync(bool archived)
            => (await DataAsync(await admin.GetAsync($"/api/v1/audit-universe/entities?search={name}&archived={(archived ? "true" : "false")}&pageSize=50")))
                .GetProperty("items").EnumerateArray().ToArray();

        // Restoring something that is not archived is refused.
        Assert.Equal(HttpStatusCode.Conflict, (await admin.PostAsync($"/api/v1/audit-universe/entities/{id}/restore", null)).StatusCode);

        // ---- Archive: gone from the live universe, present (and flagged) when archived are included.
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/audit-universe/entities/{id}")).StatusCode);
        Assert.Empty(await SearchAsync(archived: false));
        var archivedRow = Assert.Single(await SearchAsync(archived: true));
        Assert.True(archivedRow.GetProperty("isArchived").GetBoolean());
        Assert.NotEqual(JsonValueKind.Null, archivedRow.GetProperty("archivedAt").ValueKind);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync($"/api/v1/audit-universe/entities/{id}")).StatusCode);

        // ---- Restore: back, live, with its description and scores exactly as they were.
        var restored = await DataAsync(await admin.PostAsync($"/api/v1/audit-universe/entities/{id}/restore", null));
        Assert.False(restored.GetProperty("isArchived").GetBoolean());
        Assert.Equal(JsonValueKind.Null, restored.GetProperty("archivedAt").ValueKind);
        Assert.Equal("Kept through archive", restored.GetProperty("description").GetString());
        Assert.Equal(residual, restored.GetProperty("compositeResidualScore").GetDecimal());

        var liveRow = Assert.Single(await SearchAsync(archived: false));
        Assert.False(liveRow.GetProperty("isArchived").GetBoolean());
        (await admin.GetAsync($"/api/v1/audit-universe/entities/{id}")).EnsureSuccessStatusCode();

        // It is a working entity again — it can be edited…
        var edited = await DataAsync(await admin.PatchAsJsonAsync($"/api/v1/audit-universe/entities/{id}", new
        {
            name, entityType = "branch", description = "Edited after restore", version = restored.GetProperty("version").GetString(),
        }));
        Assert.Equal("Edited after restore", edited.GetProperty("description").GetString());

        // …and the restore is on the audit trail.
        var trail = await DataAsync(await admin.GetAsync($"/api/v1/audit-trail/object/audit_universe_entity/{id}"));
        var events = trail.GetProperty("items").EnumerateArray().Select(e => e.GetProperty("eventType").GetString()).ToArray();
        Assert.Contains("universe_entity_archived", events);
        Assert.Contains("universe_entity_restored", events);

        // An unknown id is a plain 404.
        Assert.Equal(HttpStatusCode.NotFound, (await admin.PostAsync($"/api/v1/audit-universe/entities/{Guid.NewGuid()}/restore", null)).StatusCode);
    }

    /// <summary>Restoring needs the same permission as archiving — a viewer cannot bring entities back.</summary>
    [Fact]
    public async Task Restoring_an_entity_requires_manage_universe()
    {
        var admin = await AdminAsync();
        var created = await DataAsync(await admin.PostAsJsonAsync("/api/v1/audit-universe/entities", new { name = $"R{Guid.NewGuid():N}", entityType = "branch" }));
        var id = created.GetProperty("id").GetGuid();
        (await admin.DeleteAsync($"/api/v1/audit-universe/entities/{id}")).EnsureSuccessStatusCode();

        var auditee = NewClient(); // the seeded 'auditee' dev user holds no role
        (await auditee.PostAsJsonAsync("/api/v1/auth/login", new { username = "auditee", password = "Passw0rd!" })).EnsureSuccessStatusCode();

        Assert.Equal(HttpStatusCode.Forbidden, (await auditee.PostAsync($"/api/v1/audit-universe/entities/{id}/restore", null)).StatusCode);
    }

    [Fact]
    public async Task Unknown_entity_type_is_rejected()
    {
        var admin = await AdminAsync();
        var response = await admin.PostAsJsonAsync("/api/v1/audit-universe/entities", new { name = "X", entityType = "not_a_type" });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Plan_lifecycle_create_add_item_submit()
    {
        var admin = await AdminAsync();

        var entity = await DataAsync(await admin.PostAsJsonAsync("/api/v1/audit-universe/entities", new { name = $"E{Guid.NewGuid():N}", entityType = "process" }));
        var entityId = entity.GetProperty("id").GetGuid();

        var plan = await DataAsync(await admin.PostAsJsonAsync("/api/v1/annual-plans", new
        {
            periodLabel = "FY2099",
            periodStart = "2099-01-01",
            periodEnd = "2099-12-31",
        }));
        var planId = plan.GetProperty("id").GetGuid();
        Assert.Equal("draft", plan.GetProperty("status").GetString());

        // Submit before items → 422 (business rule: a plan needs at least one item).
        var earlySubmit = await admin.PostAsync($"/api/v1/annual-plans/{planId}/submit", null);
        Assert.Equal(HttpStatusCode.UnprocessableContent, earlySubmit.StatusCode);

        (await admin.PostAsJsonAsync($"/api/v1/annual-plans/{planId}/items", new
        {
            entityIds = new[] { entityId },
            auditType = "process_review",
            plannedStartDate = "2099-03-01",
            plannedEndDate = "2099-03-31",
            estimatedEffortDays = 10,
        })).EnsureSuccessStatusCode();

        var submitted = await DataAsync(await admin.PostAsync($"/api/v1/annual-plans/{planId}/submit", null));
        Assert.Equal("submitted", submitted.GetProperty("status").GetString());

        // Item outside the plan period → 422.
        var badItem = await admin.PostAsJsonAsync($"/api/v1/annual-plans/{planId}/items", new
        {
            entityIds = new[] { entityId },
            auditType = "x",
            plannedStartDate = "2100-01-01",
            plannedEndDate = "2100-02-01",
        });
        Assert.Equal(HttpStatusCode.Conflict, badItem.StatusCode); // plan not editable (Submitted)
    }
}
