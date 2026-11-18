using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AuditX.Api.IntegrationTests;

/// <summary>
/// Working-paper review end to end: the lead signs answered items off and raises review notes, the team answers them,
/// the lead clears them, and only then does the audit complete. The person who answered an item cannot sign it off,
/// and the audit's auditee neither sees nor takes part in the review.
/// </summary>
public sealed class WorkingPaperReviewFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
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

    private static async Task<Dictionary<string, Guid>> UsersByEmailAsync(HttpClient admin)
    {
        var data = await DataAsync(await admin.GetAsync("/api/v1/users?limit=100"));
        return data.GetProperty("items").EnumerateArray()
            .ToDictionary(u => u.GetProperty("email").GetString()!, u => u.GetProperty("id").GetGuid());
    }

    /// <summary>A started audit led by the manager, with the admin on the team as an auditor, and one answered item.</summary>
    private async Task<(Guid AuditId, Guid ItemId)> AnsweredAuditAsync(HttpClient admin)
    {
        var users = await UsersByEmailAsync(admin);
        var created = await DataAsync(await admin.PostAsJsonAsync("/api/v1/audits", new
        {
            name = $"Review {Guid.NewGuid():N}", auditType = "operational", startDate = "2027-01-10", targetEndDate = "2027-02-10",
            leadUserId = users["manager@auditx.local"], auditeeUserId = users["auditee@auditx.local"],
        }));
        var auditId = created.GetProperty("id").GetGuid();
        var withItem = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/checklist/items",
            new { prompt = "Is dual control enforced?", responseType = "pass_fail_na", isRequired = true, version = Version(created) }));
        var withTeam = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/team",
            new { userId = users["admin@auditx.local"], teamRole = "auditor", version = Version(withItem) }));
        var planned = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition",
            new { targetState = "planned", reason = (string?)null, version = Version(withTeam) }));
        var started = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition",
            new { targetState = "in_progress", reason = (string?)null, version = Version(planned) }));
        var itemId = started.GetProperty("checklistItems")[0].GetProperty("id").GetGuid();
        await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/items/{itemId}/responses",
            new { verdict = "pass", comment = (string?)null, isDraft = false, version = Version(started) }));
        return (auditId, itemId);
    }

    [Fact]
    public async Task The_lead_reviews_the_team_answers_notes_and_the_audit_completes_once_everything_is_cleared()
    {
        var admin = await LoginAsync("admin");
        var lead = await LoginAsync("manager");
        var (auditId, itemId) = await AnsweredAuditAsync(admin);

        // The lead sees one item ready for sign-off; completion is refused while it is unreviewed.
        var review = await DataAsync(await lead.GetAsync($"/api/v1/audits/{auditId}/review/working-papers"));
        Assert.True(review.GetProperty("isRequired").GetBoolean());
        Assert.True(review.GetProperty("canReview").GetBoolean());
        Assert.Equal(1, review.GetProperty("readyForSignOff").GetInt32());
        Assert.Equal(0, review.GetProperty("signedOffItems").GetInt32());

        var tooEarly = await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition",
            new { targetState = "completed", reason = (string?)null, version = Version(review) });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, tooEarly.StatusCode);
        Assert.Equal("audit.review_incomplete", await ErrorCodeAsync(tooEarly));

        // The lead raises a note on the item; the admin (who answered it) replies.
        var raised = await DataAsync(await lead.PostAsJsonAsync($"/api/v1/audits/{auditId}/review-notes",
            new { itemId, body = "Reference the sample selection in the working paper.", version = Version(review) }));
        Assert.Equal(1, raised.GetProperty("openNotes").GetInt32());
        var noteId = raised.GetProperty("notes")[0].GetProperty("id").GetGuid();
        Assert.Equal("open", raised.GetProperty("notes")[0].GetProperty("status").GetString());

        var adminView = await DataAsync(await admin.GetAsync($"/api/v1/audits/{auditId}/review/working-papers"));
        Assert.False(adminView.GetProperty("canReview").GetBoolean());
        Assert.True(adminView.GetProperty("canAnswer").GetBoolean());

        var answered = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/review-notes/{noteId}/answer",
            new { answer = "Sample referenced in working paper 3.", version = Version(adminView) }));
        Assert.Equal(1, answered.GetProperty("answeredNotes").GetInt32());

        // The admin cannot clear notes or sign off — and could not sign off their own answer anyway.
        var notReviewer = await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/review-notes/{noteId}/clear",
            new { version = Version(answered) });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, notReviewer.StatusCode);
        Assert.Equal("audit.not_reviewer", await ErrorCodeAsync(notReviewer));

        // The lead clears the note and signs the item off.
        var cleared = await DataAsync(await lead.PostAsJsonAsync($"/api/v1/audits/{auditId}/review-notes/{noteId}/clear",
            new { version = Version(answered) }));
        Assert.Equal("cleared", cleared.GetProperty("notes")[0].GetProperty("status").GetString());
        var signedOff = await DataAsync(await lead.PostAsJsonAsync($"/api/v1/audits/{auditId}/items/{itemId}/sign-off",
            new { version = Version(cleared) }));
        Assert.Equal(1, signedOff.GetProperty("signedOffItems").GetInt32());
        var users = await UsersByEmailAsync(admin);
        Assert.Equal(users["manager@auditx.local"], signedOff.GetProperty("items")[0].GetProperty("reviewedBy").GetGuid());

        // Now the audit completes, and the review is in its history.
        var completed = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition",
            new { targetState = "completed", reason = (string?)null, version = Version(signedOff) }));
        Assert.Equal("completed", completed.GetProperty("status").GetString());

        var history = await DataAsync(await admin.GetAsync($"/api/v1/audits/{auditId}/history"));
        var events = history.EnumerateArray().Select(e => e.GetProperty("eventType").GetString()).ToList();
        Assert.Contains("review_note_raised", events);
        Assert.Contains("review_note_answered", events);
        Assert.Contains("review_note_cleared", events);
        Assert.Contains("item_signed_off", events);
    }

    [Fact]
    public async Task Nobody_signs_off_their_own_answer_and_a_stale_version_is_refused()
    {
        var admin = await LoginAsync("admin");
        var lead = await LoginAsync("manager");
        var (auditId, itemId) = await AnsweredAuditAsync(admin);
        var version = await AuditVersionAsync(admin, auditId);

        // The admin holds every permission, but they answered the item and are not a reviewer on this audit.
        var own = await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/items/{itemId}/sign-off", new { version });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, own.StatusCode);

        var first = await DataAsync(await lead.PostAsJsonAsync($"/api/v1/audits/{auditId}/review-notes",
            new { itemId = (Guid?)null, body = "Add the scope memo.", version }));
        Assert.NotEqual(version, Version(first));

        var stale = await lead.PostAsJsonAsync($"/api/v1/audits/{auditId}/review-notes",
            new { itemId = (Guid?)null, body = "A second note on an out-of-date page.", version });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
    }

    [Fact]
    public async Task The_auditee_neither_sees_nor_takes_part_in_the_review()
    {
        var admin = await LoginAsync("admin");
        var lead = await LoginAsync("manager");
        var auditee = await LoginAsync("auditee");
        var (auditId, itemId) = await AnsweredAuditAsync(admin);
        var raised = await DataAsync(await lead.PostAsJsonAsync($"/api/v1/audits/{auditId}/review-notes",
            new { itemId, body = "Internal: re-perform the count.", version = await AuditVersionAsync(admin, auditId) }));
        var noteId = raised.GetProperty("notes")[0].GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.Forbidden, (await auditee.GetAsync($"/api/v1/audits/{auditId}/review/working-papers")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await auditee.PostAsJsonAsync(
            $"/api/v1/audits/{auditId}/review-notes/{noteId}/answer", new { answer = "Done.", version = Version(raised) })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await auditee.PostAsJsonAsync(
            $"/api/v1/audits/{auditId}/items/{itemId}/sign-off", new { version = Version(raised) })).StatusCode);

        // Nor does the audit's history give the notes away; the team still sees them there.
        var auditeeHistory = await DataAsync(await auditee.GetAsync($"/api/v1/audits/{auditId}/history"));
        Assert.DoesNotContain(auditeeHistory.EnumerateArray(), e => e.GetProperty("eventType").GetString() == "review_note_raised");
        var teamHistory = await DataAsync(await admin.GetAsync($"/api/v1/audits/{auditId}/history"));
        Assert.Contains(teamHistory.EnumerateArray(), e => e.GetProperty("eventType").GetString() == "review_note_raised");
    }
}
