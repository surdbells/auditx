using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AuditX.Application.Abstractions.Notifications;
using AuditX.Application.Notifications.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AuditX.Api.IntegrationTests;

public sealed class NotificationFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
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

    private static async Task<Dictionary<string, Guid>> UsersByEmailAsync(HttpClient admin)
    {
        var data = await DataAsync(await admin.GetAsync("/api/v1/users?limit=100"));
        return data.GetProperty("items").EnumerateArray()
            .ToDictionary(u => u.GetProperty("email").GetString()!, u => u.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Event_catalogue_and_seeded_default_rules_are_available()
    {
        var admin = await AdminAsync();

        var catalogue = await DataAsync(await admin.GetAsync("/api/v1/admin/events/catalogue"));
        Assert.True(catalogue.GetArrayLength() > 0);

        var rules = await DataAsync(await admin.GetAsync("/api/v1/notification-rules"));
        var eventTypes = rules.EnumerateArray().Select(r => r.GetProperty("eventType").GetString()).ToArray();
        Assert.Contains("exception_raised", eventTypes);
    }

    [Fact]
    public async Task Admin_can_create_a_rule_override_a_template_and_preview_it()
    {
        var admin = await AdminAsync();

        var created = await DataAsync(await admin.PostAsJsonAsync("/api/v1/notification-rules", new
        {
            eventType = "audit_published",
            name = $"Custom {Guid.NewGuid():N}",
            recipientResolutionJson = "{\"type\":\"role\",\"value\":\"Audit Manager\"}",
            channelsJson = "[\"email\"]",
            templateKey = "custom_key",
            isActive = true,
        }));
        Assert.Equal("audit_published", created.GetProperty("eventType").GetString());

        var template = await DataAsync(await admin.PostAsJsonAsync("/api/v1/notification-templates", new
        {
            templateKey = "custom_key",
            channel = "email",
            subjectTemplate = "Hi {{ Name }}",
            bodyTemplate = "Audit {{ AuditId }} was published.",
        }));
        Assert.Equal("bank", template.GetProperty("scope").GetString());

        var preview = await DataAsync(await admin.PostAsJsonAsync("/api/v1/notification-rules/preview", new
        {
            recipientResolutionJson = "{\"type\":\"role\",\"value\":\"Audit Manager\"}",
            templateKey = "custom_key",
            samplePayloadJson = "{\"AuditId\":\"A-42\"}",
        }));
        Assert.Equal("Audit A-42 was published.", preview.GetProperty("renderedBody").GetString());
    }

    [Fact]
    public async Task Invalid_status_filter_is_a_422_not_a_409()
    {
        var admin = await AdminAsync();
        var response = await admin.GetAsync("/api/v1/notification-dispatches?status=not_a_status");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task User_can_read_and_update_their_notification_preferences()
    {
        var admin = await AdminAsync();

        (await admin.PatchAsJsonAsync("/api/v1/users/me/notification-preferences", new { preferencesJson = "{\"sms_non_critical\":true}" }))
            .EnsureSuccessStatusCode();

        var prefs = await DataAsync(await admin.GetAsync("/api/v1/users/me/notification-preferences"));
        Assert.Contains("sms_non_critical", prefs.GetProperty("preferencesJson").GetString());
    }

    [Fact]
    public async Task Pipeline_creates_and_delivers_a_dispatch_for_a_seeded_rule()
    {
        var admin = await AdminAsync();
        var users = await UsersByEmailAsync(admin);
        var ownerId = users["auditee@auditx.local"];

        // Run the pipeline directly (the same code the Hangfire ingest job runs) against the real DB + seeded rules.
        using (var scope = factory.Services.CreateScope())
        {
            var ingest = scope.ServiceProvider.GetRequiredService<NotificationIngestService>();
            var envelope = new DomainEventEnvelope(
                "exception_raised", Guid.CreateVersion7(), DateTimeOffset.UtcNow, null,
                $"{{\"ExceptionId\":\"{Guid.NewGuid()}\",\"AuditId\":\"{Guid.NewGuid()}\",\"Severity\":\"High\",\"OwnerUserId\":\"{ownerId}\"}}");
            await ingest.ProcessAsync(envelope, CancellationToken.None);
        }

        var dispatches = await DataAsync(await admin.GetAsync("/api/v1/notification-dispatches?eventType=exception_raised&limit=100"));
        var mine = dispatches.GetProperty("items").EnumerateArray()
            .Where(d => d.GetProperty("recipientAddress").GetString() == "auditee@auditx.local")
            .ToArray();

        Assert.NotEmpty(mine);
        Assert.Contains(mine, d => d.GetProperty("status").GetString() == "delivered");
    }
}
