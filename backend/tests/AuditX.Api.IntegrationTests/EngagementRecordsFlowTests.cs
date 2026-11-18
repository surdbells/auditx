using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AuditX.Api.IntegrationTests;

/// <summary>
/// Engagement records end to end, with the institution requiring independence declarations (the default):
/// fieldwork waits for the team's declarations; a disclosed conflict is decided by a manager other than the declarant;
/// the engagement letter is drafted, issued and acknowledged by the auditee alone; the kickoff minutes are recorded and
/// shared with the auditee; independence stays internal to the audit team.
/// </summary>
public sealed class EngagementRecordsFlowTests(StrictIndependenceApiFactory factory) : IClassFixture<StrictIndependenceApiFactory>
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
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode} from {response.RequestMessage?.RequestUri?.PathAndQuery}: {body}");
        return JsonDocument.Parse(body).RootElement.GetProperty("data").Clone();
    }

    private static async Task<string> ErrorCodeAsync(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("error_code").GetString() ?? string.Empty;

    private static string Version(JsonElement node) => node.GetProperty("version").GetString()!;

    private static async Task<string> AuditVersionAsync(HttpClient client, Guid auditId)
        => Version(await DataAsync(await client.GetAsync($"/api/v1/audits/{auditId}")));

    [Fact]
    public async Task Independence_gates_fieldwork_and_the_letter_and_kickoff_are_shared_with_the_auditee()
    {
        var admin = await LoginAsync("admin");
        var colleague = await LoginAsync("manager");
        var auditee = await LoginAsync("auditee");
        var users = (await DataAsync(await admin.GetAsync("/api/v1/users?limit=200"))).GetProperty("items").EnumerateArray()
            .ToDictionary(u => u.GetProperty("email").GetString()!, u => u.GetProperty("id").GetGuid());
        var adminId = users["admin@auditx.local"];
        var colleagueId = users["manager@auditx.local"];
        var auditeeId = users["auditee@auditx.local"];

        // A planned audit led by the administrator, with a colleague on the team as an auditor.
        var created = await DataAsync(await admin.PostAsJsonAsync("/api/v1/audits", new
        {
            name = $"Engagement {Guid.NewGuid():N}", auditType = "operational", startDate = "2027-01-10", targetEndDate = "2027-02-10",
            leadUserId = adminId, auditeeUserId = auditeeId,
        }));
        var auditId = created.GetProperty("id").GetGuid();
        var withItem = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/checklist/items",
            new { prompt = "Is dual control enforced?", responseType = "pass_fail_na", isRequired = true, version = Version(created) }));
        var withTeam = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/team",
            new { userId = colleagueId, teamRole = "auditor", version = Version(withItem) }));
        var planned = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition",
            new { targetState = "planned", reason = (string?)null, version = Version(withTeam) }));

        // Fieldwork waits for the whole team.
        var tooSoon = await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition",
            new { targetState = "in_progress", reason = (string?)null, version = Version(planned) });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, tooSoon.StatusCode);
        Assert.Equal("audit.independence_outstanding", await ErrorCodeAsync(tooSoon));

        // The lead declares; the colleague discloses a conflict.
        var afterLead = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/engagement/independence",
            new { hasConflict = false, conflictDetails = (string?)null, version = Version(planned) }));
        Assert.Equal(1, afterLead.GetProperty("independence").GetProperty("outstanding").GetInt32());
        var disclosed = await DataAsync(await colleague.PostAsJsonAsync($"/api/v1/audits/{auditId}/engagement/independence",
            new { hasConflict = true, conflictDetails = "Seconded to the area's operations team last year.", version = afterLead.GetProperty("auditVersion").GetString() }));
        var colleagueRow = disclosed.GetProperty("independence").GetProperty("members").EnumerateArray()
            .Single(m => m.GetProperty("userId").GetGuid() == colleagueId);
        Assert.Equal("conflict_disclosed", colleagueRow.GetProperty("status").GetString());
        var declarationId = colleagueRow.GetProperty("declarationId").GetGuid();

        // The auditee sees none of it.
        var auditeeView = await DataAsync(await auditee.GetAsync($"/api/v1/audits/{auditId}/engagement"));
        Assert.Equal(JsonValueKind.Null, auditeeView.GetProperty("independence").ValueKind);
        Assert.Equal(HttpStatusCode.Forbidden, (await auditee.PostAsJsonAsync($"/api/v1/audits/{auditId}/engagement/independence",
            new { hasConflict = false, conflictDetails = (string?)null, version = disclosed.GetProperty("auditVersion").GetString() })).StatusCode);

        // Nobody decides on their own conflict; the lead (a manager of the audit) accepts it with safeguards.
        var leadView = await DataAsync(await admin.GetAsync($"/api/v1/audits/{auditId}/engagement"));
        Assert.True(leadView.GetProperty("independence").GetProperty("canDecide").GetBoolean());
        Assert.Equal("Seconded to the area's operations team last year.", leadView.GetProperty("independence").GetProperty("members").EnumerateArray()
            .Single(m => m.GetProperty("userId").GetGuid() == colleagueId).GetProperty("conflictDetails").GetString());
        var accepted = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/engagement/independence/{declarationId}/accept",
            new { safeguards = "The colleague does not test the operations they worked in; the lead reviews all their items.", version = leadView.GetProperty("auditVersion").GetString() }));
        Assert.True(accepted.GetProperty("independence").GetProperty("complete").GetBoolean());

        var started = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition",
            new { targetState = "in_progress", reason = (string?)null, version = accepted.GetProperty("auditVersion").GetString() }));
        Assert.Equal("in_progress", started.GetProperty("status").GetString());

        // The engagement letter: a draft stays with the team; once issued, the auditee alone acknowledges it.
        var drafted = await DataAsync(await admin.PutAsJsonAsync($"/api/v1/audits/{auditId}/engagement/letter",
            new { body = "Objectives, scope and timing of the audit.", letterVersion = (string?)null }));
        Assert.Equal("draft", drafted.GetProperty("letter").GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, (await DataAsync(await auditee.GetAsync($"/api/v1/audits/{auditId}/engagement"))).GetProperty("letter").ValueKind);

        var issued = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/engagement/letter/issue",
            new { letterVersion = Version(drafted.GetProperty("letter")) }));
        var letterId = issued.GetProperty("letter").GetProperty("id").GetGuid();
        var letterVersion = Version(issued.GetProperty("letter"));

        var auditeeLetter = await DataAsync(await auditee.GetAsync($"/api/v1/audits/{auditId}/engagement"));
        Assert.True(auditeeLetter.GetProperty("canAcknowledge").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.PostAsJsonAsync($"/api/v1/engagement-letters/{letterId}/acknowledge",
            new { comment = (string?)null, letterVersion })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await auditee.PutAsJsonAsync($"/api/v1/audits/{auditId}/engagement/letter",
            new { body = "Rewritten by the auditee.", letterVersion })).StatusCode);

        var acknowledged = await DataAsync(await auditee.PostAsJsonAsync($"/api/v1/engagement-letters/{letterId}/acknowledge",
            new { comment = "Our contact for payments is the Head of Payments.", letterVersion }));
        Assert.Equal("acknowledged", acknowledged.GetProperty("letter").GetProperty("status").GetString());
        Assert.Equal(auditeeId, acknowledged.GetProperty("letter").GetProperty("acknowledgedBy").GetGuid());

        // The kickoff minutes, shared with the auditee. The exit meeting waits for a draft report under review.
        var kickoff = await DataAsync(await admin.PutAsJsonAsync($"/api/v1/audits/{auditId}/engagement/meetings/kickoff", new
        {
            heldAt = DateTimeOffset.UtcNow.AddHours(-1),
            attendeeUserIds = new[] { adminId, auditeeId },
            externalAttendees = new[] { "J. Bello (Head of Payments)" },
            minutes = "Walked through the objectives, scope and timetable.",
            agreedPoints = new[] { "Documents within five working days." },
            reportId = (Guid?)null,
            meetingVersion = (string?)null,
        }));
        Assert.Equal("kickoff", kickoff.GetProperty("meetings")[0].GetProperty("kind").GetString());
        var shared = (await DataAsync(await auditee.GetAsync($"/api/v1/audits/{auditId}/engagement"))).GetProperty("meetings");
        Assert.Equal("Walked through the objectives, scope and timetable.", shared[0].GetProperty("minutes").GetString());

        var exitTooSoon = await admin.PutAsJsonAsync($"/api/v1/audits/{auditId}/engagement/meetings/exit", new
        {
            heldAt = DateTimeOffset.UtcNow.AddHours(-1), attendeeUserIds = new[] { adminId }, externalAttendees = Array.Empty<string>(),
            minutes = "Findings discussed.", agreedPoints = Array.Empty<string>(), reportId = Guid.NewGuid(), meetingVersion = (string?)null,
        });
        Assert.Equal(HttpStatusCode.Conflict, exitTooSoon.StatusCode);
        Assert.Equal("meeting.not_yet", await ErrorCodeAsync(exitTooSoon));

        // The history: the auditee sees the letter and the meeting, never the independence record; the team sees all.
        var auditeeHistory = (await DataAsync(await auditee.GetAsync($"/api/v1/audits/{auditId}/history"))).EnumerateArray()
            .Select(e => e.GetProperty("eventType").GetString()).ToList();
        Assert.Contains("engagement_letter_issued", auditeeHistory);
        Assert.Contains("audit_meeting_recorded", auditeeHistory);
        Assert.DoesNotContain("independence_declared", auditeeHistory);
        Assert.DoesNotContain("independence_conflict_accepted", auditeeHistory);
        var teamHistory = (await DataAsync(await admin.GetAsync($"/api/v1/audits/{auditId}/history"))).EnumerateArray()
            .Select(e => e.GetProperty("eventType").GetString()).ToList();
        Assert.Contains("independence_conflict_accepted", teamHistory);
    }

    [Fact]
    public async Task A_letter_edited_after_issue_becomes_a_new_revision_and_stale_versions_are_refused()
    {
        var admin = await LoginAsync("admin");
        var users = (await DataAsync(await admin.GetAsync("/api/v1/users?limit=200"))).GetProperty("items").EnumerateArray()
            .ToDictionary(u => u.GetProperty("email").GetString()!, u => u.GetProperty("id").GetGuid());
        var created = await DataAsync(await admin.PostAsJsonAsync("/api/v1/audits", new
        {
            name = $"Letter {Guid.NewGuid():N}", auditType = "operational", startDate = "2027-01-10", targetEndDate = "2027-02-10",
            leadUserId = users["admin@auditx.local"], auditeeUserId = users["auditee@auditx.local"],
        }));
        var auditId = created.GetProperty("id").GetGuid();

        var drafted = await DataAsync(await admin.PutAsJsonAsync($"/api/v1/audits/{auditId}/engagement/letter",
            new { body = "First revision.", letterVersion = (string?)null }));
        var issued = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/engagement/letter/issue",
            new { letterVersion = Version(drafted.GetProperty("letter")) }));

        var stale = await admin.PutAsJsonAsync($"/api/v1/audits/{auditId}/engagement/letter",
            new { body = "Second revision.", letterVersion = Version(drafted.GetProperty("letter")) });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        var revised = await DataAsync(await admin.PutAsJsonAsync($"/api/v1/audits/{auditId}/engagement/letter",
            new { body = "Second revision: the scope now includes FX settlements.", letterVersion = Version(issued.GetProperty("letter")) }));
        var letter = revised.GetProperty("letter");
        Assert.Equal(2, letter.GetProperty("revision").GetInt32());
        Assert.Equal("draft", letter.GetProperty("status").GetString());
        var previous = Assert.Single(letter.GetProperty("previousRevisions").EnumerateArray());
        Assert.Equal("First revision.", previous.GetProperty("body").GetString());
        Assert.NotEqual(JsonValueKind.Null, previous.GetProperty("issuedAt").ValueKind);
    }
}
