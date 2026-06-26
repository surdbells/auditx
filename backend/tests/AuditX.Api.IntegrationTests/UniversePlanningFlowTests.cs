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

        // Submit before items → 409.
        var earlySubmit = await admin.PostAsync($"/api/v1/annual-plans/{planId}/submit", null);
        Assert.Equal(HttpStatusCode.Conflict, earlySubmit.StatusCode);

        await admin.PostAsJsonAsync($"/api/v1/annual-plans/{planId}/items", new
        {
            entityId,
            auditType = "process_review",
            plannedStartDate = "2099-03-01",
            plannedEndDate = "2099-03-31",
            estimatedEffortDays = 10,
        });

        var submitted = await DataAsync(await admin.PostAsync($"/api/v1/annual-plans/{planId}/submit", null));
        Assert.Equal("submitted", submitted.GetProperty("status").GetString());

        // Item outside the plan period → 422.
        var badItem = await admin.PostAsJsonAsync($"/api/v1/annual-plans/{planId}/items", new
        {
            entityId,
            auditType = "x",
            plannedStartDate = "2100-01-01",
            plannedEndDate = "2100-02-01",
        });
        Assert.Equal(HttpStatusCode.Conflict, badItem.StatusCode); // plan not editable (Submitted)
    }
}
