using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AuditX.Api.IntegrationTests;

/// <summary>
/// D3-A saved views: create private + shared views, owner-vs-shared visibility, owner-only edit/delete,
/// JSON-object validation and the friendly duplicate-name conflict.
/// </summary>
public sealed class SavedViewFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
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

    private static string Str(JsonElement e, string prop) => e.GetProperty(prop).GetString()!;

    [Fact]
    public async Task Create_share_visibility_owner_scope_and_validation()
    {
        var key = $"exceptions-{Guid.NewGuid():N}"; // isolate this test's views on the shared class DB.
        var manager = await LoginAsync("manager");
        var auditor = await LoginAsync("auditor");

        // Manager creates one private and one shared view.
        var priv = await DataAsync(await manager.PostAsJsonAsync("/api/v1/saved-views", new
        {
            viewKey = key, name = "My criticals", parametersJson = "{\"severity\":\"critical\"}", isShared = false,
        }));
        Assert.True(priv.GetProperty("isOwner").GetBoolean());
        Assert.False(priv.GetProperty("isShared").GetBoolean());

        var shared = await DataAsync(await manager.PostAsJsonAsync("/api/v1/saved-views", new
        {
            viewKey = key, name = "Team overdue", parametersJson = "{\"overdue\":true}", isShared = true,
        }));

        // Manager sees both of their views.
        var mineList = await DataAsync(await manager.GetAsync($"/api/v1/saved-views?viewKey={key}"));
        Assert.Equal(2, mineList.GetArrayLength());

        // Auditor sees only the shared view — and it is not theirs.
        var auditorList = await DataAsync(await auditor.GetAsync($"/api/v1/saved-views?viewKey={key}"));
        Assert.Equal(1, auditorList.GetArrayLength());
        var seen = auditorList[0];
        Assert.Equal("Team overdue", Str(seen, "name"));
        Assert.True(seen.GetProperty("isShared").GetBoolean());
        Assert.False(seen.GetProperty("isOwner").GetBoolean());

        // A non-owner cannot edit or delete a shared view.
        var forbiddenEdit = await auditor.PatchAsJsonAsync($"/api/v1/saved-views/{shared.GetProperty("id").GetGuid()}",
            new { name = "Hijacked", parametersJson = "{}", isShared = true, version = Str(shared, "version") });
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenEdit.StatusCode);

        var forbiddenDelete = await auditor.DeleteAsync($"/api/v1/saved-views/{shared.GetProperty("id").GetGuid()}?version={Uri.EscapeDataString(Str(shared, "version"))}");
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenDelete.StatusCode);

        // The owner can rename their view (rowversion echoed).
        var renamed = await DataAsync(await manager.PatchAsJsonAsync($"/api/v1/saved-views/{priv.GetProperty("id").GetGuid()}",
            new { name = "My criticals v2", parametersJson = "{\"severity\":\"high\"}", isShared = false, version = Str(priv, "version") }));
        Assert.Equal("My criticals v2", Str(renamed, "name"));

        // Duplicate name on the same screen is a friendly 409, not a 500.
        var dup = await manager.PostAsJsonAsync("/api/v1/saved-views", new
        {
            viewKey = key, name = "Team overdue", parametersJson = "{}", isShared = false,
        });
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);

        // Parameters must be a JSON object → 422.
        var badJson = await manager.PostAsJsonAsync("/api/v1/saved-views", new
        {
            viewKey = key, name = "Bad", parametersJson = "not-json", isShared = false,
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, badJson.StatusCode);

        // The owner can delete their view.
        var del = await manager.DeleteAsync($"/api/v1/saved-views/{renamed.GetProperty("id").GetGuid()}?version={Uri.EscapeDataString(Str(renamed, "version"))}");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);

        var afterDelete = await DataAsync(await manager.GetAsync($"/api/v1/saved-views?viewKey={key}"));
        Assert.Equal(1, afterDelete.GetArrayLength()); // only the shared one remains
    }

    [Fact]
    public async Task Saved_views_require_authentication()
    {
        var anon = NewClient();
        var response = await anon.GetAsync("/api/v1/saved-views?viewKey=exceptions");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
