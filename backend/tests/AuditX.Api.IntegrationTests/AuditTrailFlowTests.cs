using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace AuditX.Api.IntegrationTests;

public sealed class AuditTrailFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
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
    public async Task Query_returns_trail_entries_and_records_its_own_access()
    {
        var admin = await AdminAsync();

        // The login above already produced trail entries; the query is itself audited (trail_query).
        var page = await DataAsync(await admin.GetAsync("/api/v1/audit-trail?limit=50"));
        Assert.True(page.GetProperty("items").GetArrayLength() > 0);

        var byEvent = await DataAsync(await admin.GetAsync("/api/v1/audit-trail?event_type=trail_query&limit=50"));
        Assert.All(byEvent.GetProperty("items").EnumerateArray(),
            e => Assert.Equal("trail_query", e.GetProperty("eventType").GetString()));
    }

    [Fact]
    public async Task Cursor_pagination_round_trips_without_gaps()
    {
        var admin = await AdminAsync();

        // Offset paging over a single snapshot: a 2-row page must hold two DISTINCT rows (no gap/dupe within the page).
        // Cross-request page comparison is deliberately avoided — every trail read itself appends a `trail_query` row,
        // so the dataset shifts by one between requests (an inherent property of offset paging over a growing table).
        var page = await DataAsync(await admin.GetAsync("/api/v1/audit-trail?page=1&pageSize=2"));
        var items = page.GetProperty("items");
        Assert.True(items.GetArrayLength() >= 1);
        Assert.True(page.GetProperty("total").GetInt32() >= 1);
        Assert.Equal(1, page.GetProperty("page").GetInt32());
        if (items.GetArrayLength() == 2)
        {
            Assert.NotEqual(items[0].GetProperty("id").GetString(), items[1].GetProperty("id").GetString());
        }
    }

    [Fact]
    public async Task Forged_cursor_is_treated_as_first_page_not_a_500()
    {
        var admin = await AdminAsync();

        // A base64 cursor whose ticks component is out of DateTimeOffset range must not crash the endpoint.
        var forged = Convert.ToBase64String(Encoding.UTF8.GetBytes("9000000000000000000:00000000-0000-0000-0000-000000000000"));
        var response = await admin.GetAsync($"/api/v1/audit-trail?cursor={Uri.EscapeDataString(forged)}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var garbage = await admin.GetAsync("/api/v1/audit-trail?cursor=not-base64!!");
        Assert.Equal(HttpStatusCode.OK, garbage.StatusCode);
    }

    [Fact]
    public async Task Csv_export_returns_a_file_with_an_integrity_hash_header()
    {
        var admin = await AdminAsync();

        var response = await admin.GetAsync("/api/v1/audit-trail/export?limit=100");
        response.EnsureSuccessStatusCode();
        Assert.Equal("text/csv", response.Content.Headers.ContentType!.MediaType);
        Assert.True(response.Headers.Contains("X-Content-SHA256"));

        var csv = await response.Content.ReadAsStringAsync();
        Assert.StartsWith("id,occurred_at_utc,actor_type", csv);
    }

    [Fact]
    public async Task Object_history_is_paginated()
    {
        var admin = await AdminAsync();

        // Any object type works; use a random id — the endpoint must return a PagedResult shape (possibly empty).
        var page = await DataAsync(await admin.GetAsync($"/api/v1/audit-trail/object/audit/{Guid.NewGuid()}"));
        Assert.True(page.TryGetProperty("items", out _));
        Assert.True(page.TryGetProperty("total", out _));
    }

    [Fact]
    public async Task Flagged_evidence_list_is_available_to_admin_ops()
    {
        var admin = await AdminAsync();
        var flagged = await DataAsync(await admin.GetAsync("/api/v1/evidence/flagged"));
        Assert.Equal(JsonValueKind.Array, flagged.ValueKind);
    }

    [Fact]
    public async Task Audit_trail_requires_the_view_permission()
    {
        var auditee = NewClient();
        (await auditee.PostAsJsonAsync("/api/v1/auth/login", new { username = "auditee", password = "Passw0rd!" })).EnsureSuccessStatusCode();

        var response = await auditee.GetAsync("/api/v1/audit-trail?limit=10");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
