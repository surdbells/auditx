using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AuditX.Api.BackgroundJobs;
using AuditX.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AuditX.Api.IntegrationTests;

/// <summary>
/// Action follow-up end to end: the people remediating a finding ask for more time and the audit side decides; the
/// worklist shows each person what waits on them; reminders run through an action's life; and a failed follow-up
/// verification reopens the finding with a new date.
/// </summary>
public sealed class ActionFollowUpFlowTests(ApiFactory factory) : AccessModelTestBase(factory), IClassFixture<ApiFactory>
{
    private static readonly DateOnly Today = DateOnly.FromDateTime(DateTime.UtcNow);

    private static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd");

    /// <summary>
    /// Admin leads an audit of the auditee, raises a finding the auditee owns, and the auditee writes a one-action plan
    /// due in <paramref name="actionDueInDays"/> days. The plan is then approved directly (approval is maker-checker gated
    /// through the API, which is covered elsewhere).
    /// </summary>
    private async Task<(HttpClient Admin, HttpClient Auditee, Guid FindingId, Guid ActionId)> ApprovedPlanAsync(int actionDueInDays)
    {
        var admin = await LoginAsync("admin");
        var users = await UsersByEmailAsync(admin);
        var audit = await SeedStartedAuditAsync(admin, users, "admin@auditx.local", "auditee@auditx.local", "auditor@auditx.local");
        var ownerId = users["auditee@auditx.local"];
        var finding = await FailAndRaiseAsync(admin, audit, ownerId);
        var findingId = finding.GetProperty("id").GetGuid();

        var auditee = await LoginAsync("auditee");
        await DataAsync(await auditee.PostAsJsonAsync($"/api/v1/exceptions/{findingId}/map", new
        {
            actions = new[]
            {
                new { description = "Introduce dual control", ownerUserId = ownerId, targetDate = Iso(Today.AddDays(actionDueInDays)), expectedEvidenceType = "screenshot" },
            },
            version = Version(finding),
        }));

        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var entity = await db.Exceptions.Include(e => e.MapActions).FirstAsync(e => e.Id == findingId);
            entity.ApproveMap(users["admin@auditx.local"], DateTimeOffset.UtcNow);
            await db.SaveChangesAsync();
        }

        var approved = await GetAsync(admin, findingId);
        Assert.Equal("map_approved", approved.GetProperty("status").GetString());
        return (admin, auditee, findingId, approved.GetProperty("mapActions")[0].GetProperty("id").GetGuid());
    }

    private static async Task<JsonElement> GetAsync(HttpClient client, Guid findingId)
        => await DataAsync(await client.GetAsync($"/api/v1/exceptions/{findingId}"));

    private static async Task<JsonElement[]> MyActionsAsync(HttpClient client)
        => [.. (await DataAsync(await client.GetAsync("/api/v1/my/actions"))).EnumerateArray()];

    [Fact]
    public async Task The_owner_asks_for_more_time_and_the_audit_side_decides()
    {
        var (admin, auditee, findingId, actionId) = await ApprovedPlanAsync(10);
        var finding = await GetAsync(auditee, findingId);

        // The action's owner asks for ten more days.
        var asked = await DataAsync(await auditee.PostAsJsonAsync($"/api/v1/exceptions/{findingId}/extension-requests", new
        {
            actionId, requestedDate = Iso(Today.AddDays(20)), reason = "The vendor moved the patch to the next release window.", version = Version(finding),
        }));
        var request = asked.GetProperty("extensionRequests")[0];
        Assert.Equal("pending", request.GetProperty("status").GetString());
        var requestId = request.GetProperty("id").GetGuid();

        // A second request for the same date is refused while the first is pending.
        var again = await auditee.PostAsJsonAsync($"/api/v1/exceptions/{findingId}/extension-requests", new
        {
            actionId, requestedDate = Iso(Today.AddDays(25)), reason = "Asking again for an even later date, just in case.", version = Version(asked),
        });
        Assert.Equal(HttpStatusCode.UnprocessableContent, again.StatusCode);

        // Each side sees it in their worklist: the owner as an action with a pending request, the raiser as a decision.
        var mine = await MyActionsAsync(auditee);
        Assert.Contains(mine, i => i.GetProperty("kind").GetString() == "complete_action"
            && i.GetProperty("actionId").GetGuid() == actionId && i.GetProperty("extensionPending").GetBoolean());
        var raiser = await MyActionsAsync(admin);
        Assert.Contains(raiser, i => i.GetProperty("kind").GetString() == "decide_extension"
            && i.GetProperty("extensionRequestId").GetGuid() == requestId);

        // Management cannot grant itself the time.
        var byOwner = await auditee.PostAsJsonAsync($"/api/v1/exceptions/{findingId}/extension-requests/{requestId}/decision",
            new { approve = true, note = (string?)null, version = Version(asked) });
        Assert.Equal(HttpStatusCode.Forbidden, byOwner.StatusCode);

        // A rejection needs a reason.
        var bare = await admin.PostAsJsonAsync($"/api/v1/exceptions/{findingId}/extension-requests/{requestId}/decision",
            new { approve = false, note = (string?)null, version = Version(asked) });
        Assert.Equal(HttpStatusCode.UnprocessableContent, bare.StatusCode);

        var approved = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/exceptions/{findingId}/extension-requests/{requestId}/decision",
            new { approve = true, note = "Agreed: the interim control stays in place.", version = Version(asked) }));
        Assert.Equal("approved", approved.GetProperty("extensionRequests")[0].GetProperty("status").GetString());
        Assert.Equal(Iso(Today.AddDays(20)), approved.GetProperty("mapActions")[0].GetProperty("targetDate").GetString());

        // The decision has left the raiser's worklist.
        Assert.DoesNotContain(await MyActionsAsync(admin), i => i.GetProperty("kind").GetString() == "decide_extension"
            && i.GetProperty("extensionRequestId").GetGuid() == requestId);
    }

    [Fact]
    public async Task Nobody_decides_their_own_request_but_may_withdraw_it()
    {
        var (admin, _, findingId, _) = await ApprovedPlanAsync(10);
        var finding = await GetAsync(admin, findingId);

        // A manager of the audit may ask on the business's behalf for the finding's own date…
        var asked = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/exceptions/{findingId}/extension-requests", new
        {
            actionId = (Guid?)null, requestedDate = Iso(Today.AddDays(120)), reason = "Remediation depends on next year's budget approval.",
            version = Version(finding),
        }));
        var requestId = asked.GetProperty("extensionRequests")[0].GetProperty("id").GetGuid();

        // …but not then approve it.
        var self = await admin.PostAsJsonAsync($"/api/v1/exceptions/{findingId}/extension-requests/{requestId}/decision",
            new { approve = true, note = (string?)null, version = Version(asked) });
        Assert.Equal(HttpStatusCode.Forbidden, self.StatusCode);

        var withdrawn = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/exceptions/{findingId}/extension-requests/{requestId}/withdraw",
            new { version = Version(asked) }));
        Assert.Equal("withdrawn", withdrawn.GetProperty("extensionRequests")[0].GetProperty("status").GetString());
        Assert.Equal(finding.GetProperty("targetDate").GetString(), withdrawn.GetProperty("targetDate").GetString());
    }

    [Fact]
    public async Task Reminders_run_through_the_action_life_and_an_extension_starts_them_again()
    {
        var (admin, auditee, findingId, actionId) = await ApprovedPlanAsync(3);

        await RunReminderJobAsync();
        var reminded = await GetAsync(admin, findingId);
        Assert.Equal("due_soon", reminded.GetProperty("mapActions")[0].GetProperty("reminderStage").GetString());

        // Running again sends nothing new.
        await RunReminderJobAsync();
        Assert.Equal("due_soon", (await GetAsync(admin, findingId)).GetProperty("mapActions")[0].GetProperty("reminderStage").GetString());

        // More time is granted: the sequence starts over for the new date.
        var asked = await DataAsync(await auditee.PostAsJsonAsync($"/api/v1/exceptions/{findingId}/extension-requests", new
        {
            actionId, requestedDate = Iso(Today.AddDays(30)), reason = "The fix is built and waits for the change window.", version = Version(reminded),
        }));
        var requestId = asked.GetProperty("extensionRequests")[0].GetProperty("id").GetGuid();
        var approved = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/exceptions/{findingId}/extension-requests/{requestId}/decision",
            new { approve = true, note = (string?)null, version = Version(asked) }));
        Assert.Equal("none", approved.GetProperty("mapActions")[0].GetProperty("reminderStage").GetString());
    }

    [Fact]
    public async Task A_failed_follow_up_reopens_the_finding_with_a_new_date()
    {
        var (admin, auditee, findingId, _) = await ApprovedPlanAsync(10);

        // Remediate and close directly (evidence upload and closure are covered elsewhere).
        using (var scope = Factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var entity = await db.Exceptions.Include(e => e.MapActions).FirstAsync(e => e.Id == findingId);
            var now = DateTimeOffset.UtcNow;
            entity.MarkMapActionComplete(entity.MapActions[0].Id, requireEvidence: false, hasEvidence: true, entity.OwnerUserId, now);
            entity.MarkMapComplete(entity.OwnerUserId, now);
            entity.Close("Remediated and evidenced.", Guid.NewGuid(), now);
            await db.SaveChangesAsync();
        }

        var closed = await GetAsync(admin, findingId);
        Assert.Equal("closed", closed.GetProperty("status").GetString());

        // A failure needs to say what was found and when the business must fix it by.
        var noDate = await admin.PostAsJsonAsync($"/api/v1/exceptions/{findingId}/verifications", new
        {
            result = "failed", notes = "Two payments in the sample bypassed dual control.", version = Version(closed),
        });
        Assert.Equal(HttpStatusCode.UnprocessableContent, noDate.StatusCode);

        var newTarget = Iso(Today.AddDays(60));
        var reopened = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/exceptions/{findingId}/verifications", new
        {
            result = "failed", notes = "Two payments in the sample bypassed dual control.", version = Version(closed), newTargetDate = newTarget,
        }));
        Assert.Equal("open", reopened.GetProperty("status").GetString());
        Assert.Equal(1, reopened.GetProperty("reopenCount").GetInt32());
        Assert.Equal(newTarget, reopened.GetProperty("targetDate").GetString());
        Assert.Equal("superseded", reopened.GetProperty("mapActions")[0].GetProperty("status").GetString());
        Assert.Equal("failed", reopened.GetProperty("verifications")[0].GetProperty("result").GetString());

        // The owner is asked for a new plan.
        Assert.Contains(await MyActionsAsync(auditee), i => i.GetProperty("kind").GetString() == "submit_plan"
            && i.GetProperty("exceptionId").GetGuid() == findingId);
    }

    private async Task RunReminderJobAsync()
    {
        using var scope = Factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<MapActionReminderJob>().RunAsync(CancellationToken.None);
    }
}
