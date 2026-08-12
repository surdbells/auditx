using System.Net.Http.Json;
using System.Text.Json;
using AuditX.Application.Abstractions.Notifications;
using AuditX.Application.Notifications.Services;
using Microsoft.Extensions.DependencyInjection;

namespace AuditX.Api.IntegrationTests;

/// <summary>
/// M4 pre-audit kickoff meeting (Phase 7): scheduling a kickoff records the meeting on the audit and drives a
/// pre-audit notification to the auditee. Also covers the Teams broadcast channel — one post per (event, rule),
/// delivered via the development Teams sink.
/// </summary>
public sealed class KickoffFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
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
    public async Task Scheduling_a_kickoff_records_the_meeting_on_the_audit()
    {
        var admin = await AdminAsync();
        var users = await UsersByEmailAsync(admin);

        var created = await DataAsync(await admin.PostAsJsonAsync("/api/v1/audits", new
        {
            name = $"Kickoff {Guid.NewGuid():N}",
            auditType = "branch",
            startDate = "2027-08-10",
            targetEndDate = "2027-09-10",
            leadUserId = users["manager@auditx.local"],
            auditeeUserId = users["auditee@auditx.local"],
        }));
        var auditId = created.GetProperty("id").GetGuid();

        var when = DateTimeOffset.UtcNow.AddDays(7);
        var scheduled = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/kickoff", new
        {
            scheduledAtUtc = when,
            location = "Teams: https://teams.microsoft.com/l/meetup",
            agenda = "Scope walkthrough and evidence expectations",
            version = Version(created),
        }));

        Assert.Equal("Teams: https://teams.microsoft.com/l/meetup", scheduled.GetProperty("kickoffLocation").GetString());
        Assert.Equal("Scope walkthrough and evidence expectations", scheduled.GetProperty("kickoffAgenda").GetString());
        Assert.True(scheduled.TryGetProperty("kickoffScheduledAtUtc", out var at) && at.ValueKind != JsonValueKind.Null);
    }

    [Fact]
    public async Task A_past_kickoff_time_is_rejected()
    {
        var admin = await AdminAsync();
        var users = await UsersByEmailAsync(admin);

        var created = await DataAsync(await admin.PostAsJsonAsync("/api/v1/audits", new
        {
            name = $"Kickoff past {Guid.NewGuid():N}",
            auditType = "branch",
            startDate = "2027-08-10",
            targetEndDate = "2027-09-10",
            leadUserId = users["manager@auditx.local"],
            auditeeUserId = users["auditee@auditx.local"],
        }));
        var auditId = created.GetProperty("id").GetGuid();

        var response = await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/kickoff", new
        {
            scheduledAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
            location = (string?)null,
            agenda = (string?)null,
            version = Version(created),
        });
        Assert.Equal(System.Net.HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task Kickoff_event_notifies_the_auditee_and_posts_one_teams_broadcast()
    {
        var admin = await AdminAsync();
        var users = await UsersByEmailAsync(admin);
        var auditeeId = users["auditee@auditx.local"];
        var eventId = Guid.CreateVersion7();

        // Run the seeded audit_kickoff_scheduled rule directly (the code the Hangfire job runs) against the real DB.
        using (var scope = factory.Services.CreateScope())
        {
            var ingest = scope.ServiceProvider.GetRequiredService<NotificationIngestService>();
            var envelope = new DomainEventEnvelope(
                "audit_kickoff_scheduled", eventId, DateTimeOffset.UtcNow, null,
                $"{{\"AuditId\":\"{Guid.NewGuid()}\",\"AuditName\":\"Branch review\",\"ScheduledAtUtc\":\"2027-08-01T09:00:00Z\",\"Location\":\"Room 3\",\"AuditeeUserId\":\"{auditeeId}\",\"LeadUserId\":\"{Guid.NewGuid()}\"}}");
            await ingest.ProcessAsync(envelope, CancellationToken.None);
        }

        var dispatches = await DataAsync(await admin.GetAsync("/api/v1/notification-dispatches?eventType=audit_kickoff_scheduled&pageSize=100"));
        var forThisEvent = dispatches.GetProperty("items").EnumerateArray()
            .Where(d => d.GetProperty("eventId").GetGuid() == eventId)
            .ToArray();

        // The auditee gets an email, delivered via the dev sink.
        var email = forThisEvent.Single(d => d.GetProperty("channel").GetString() == "email");
        Assert.Equal("auditee@auditx.local", email.GetProperty("recipientAddress").GetString());
        Assert.Equal("delivered", email.GetProperty("status").GetString());

        // Teams is a single broadcast for the (event, rule): addressed to the channel reference, no recipient user.
        var teams = forThisEvent.Where(d => d.GetProperty("channel").GetString() == "teams").ToArray();
        Assert.Single(teams);
        Assert.Equal("teams:default", teams[0].GetProperty("recipientAddress").GetString());
        Assert.True(teams[0].GetProperty("recipientUserId").ValueKind == JsonValueKind.Null);
        Assert.Equal("delivered", teams[0].GetProperty("status").GetString());
    }
}
