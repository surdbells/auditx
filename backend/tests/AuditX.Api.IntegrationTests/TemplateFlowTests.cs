using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AuditX.Api.IntegrationTests;

public sealed class TemplateFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private HttpClient NewClient() => factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
    {
        HandleCookies = true,
    });

    private async Task<HttpClient> LoggedInAsync(string username)
    {
        var client = NewClient();
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", new { username, password = "Passw0rd!" });
        response.EnsureSuccessStatusCode();
        return client;
    }

    private static async Task<JsonElement> DataAsync(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("data").Clone();
    }

    [Fact]
    public async Task Full_authoring_publish_and_maker_checker_approval_flow()
    {
        var admin = await LoggedInAsync("admin");

        // Create a draft template.
        var created = await DataAsync(await admin.PostAsJsonAsync("/api/v1/templates", new
        {
            name = $"Branch Audit {Guid.NewGuid():N}",
            auditType = "branch",
            description = "Standard branch operational audit",
        }));
        var templateId = created.GetProperty("id").GetGuid();
        Assert.Equal("draft", created.GetProperty("status").GetString());

        // Add an item.
        await admin.PostAsJsonAsync($"/api/v1/templates/{templateId}/items", new
        {
            prompt = "Is cash counted daily?",
            referenceNotes = "Check 5 days of records",
            responseType = "pass_fail_na",
            sectionName = (string?)null,
            isRequired = true,
            defaultAssignmentRuleJson = (string?)null,
        });

        // Default template list returns Published only — the draft must not appear.
        var list = await DataAsync(await admin.GetAsync("/api/v1/templates"));
        Assert.DoesNotContain(list.GetProperty("items").EnumerateArray(), t => t.GetProperty("id").GetGuid() == templateId);

        // Publish is gated by maker-checker (template_publish) → 202 pending.
        var publish = await admin.PostAsync($"/api/v1/templates/{templateId}/publish", null);
        Assert.Equal(HttpStatusCode.Accepted, publish.StatusCode);
        var pendingId = (await DataAsync(publish)).GetProperty("pendingActionId").GetGuid();

        // The maker cannot approve their own publish.
        var selfApprove = await admin.PostAsync($"/api/v1/maker-checker/{pendingId}/approve", null);
        Assert.Equal(HttpStatusCode.Forbidden, selfApprove.StatusCode);

        // A different user approves; the publish executes atomically.
        var manager = await LoggedInAsync("manager");
        var approve = await manager.PostAsync($"/api/v1/maker-checker/{pendingId}/approve", null);
        Assert.Equal(HttpStatusCode.NoContent, approve.StatusCode);

        // The template is now Published with version 1.
        var afterPublish = await DataAsync(await admin.GetAsync($"/api/v1/templates/{templateId}"));
        Assert.Equal("published", afterPublish.GetProperty("status").GetString());
        Assert.Equal(1, afterPublish.GetProperty("versions").GetArrayLength());

        // Published templates reject in-place edits.
        var edit = await admin.PostAsJsonAsync($"/api/v1/templates/{templateId}/items", new
        {
            prompt = "Another item",
            responseType = "pass_fail_na",
            isRequired = false,
        });
        Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);
    }
}
