using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AuditX.Api.IntegrationTests;

public sealed class ExceptionFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
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

    private static string Version(JsonElement node) => node.GetProperty("version").GetString()!;

    private async Task<string> AuditVersionAsync(HttpClient admin, Guid auditId)
        => Version(await DataAsync(await admin.GetAsync($"/api/v1/audits/{auditId}")));

    private static async Task<Dictionary<string, Guid>> UsersByEmailAsync(HttpClient admin)
    {
        var data = await DataAsync(await admin.GetAsync("/api/v1/users?limit=100"));
        return data.GetProperty("items").EnumerateArray()
            .ToDictionary(u => u.GetProperty("email").GetString()!, u => u.GetProperty("id").GetGuid());
    }

    /// <summary>Build an in-progress audit with two responded items: item0 = Fail, item1 = Pass.</summary>
    private async Task<(Guid AuditId, Guid FailItem, Guid PassItem, Guid Owner)> SeedAuditWithResponsesAsync(HttpClient admin)
    {
        var users = await UsersByEmailAsync(admin);
        var created = await DataAsync(await admin.PostAsJsonAsync("/api/v1/audits", new
        {
            name = $"Exc {Guid.NewGuid():N}",
            auditType = "branch",
            startDate = "2027-01-10",
            targetEndDate = "2027-02-10",
            leadUserId = users["manager@auditx.local"],
            auditeeUserId = users["auditee@auditx.local"],
        }));
        var auditId = created.GetProperty("id").GetGuid();
        var version = Version(created);

        var a1 = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/checklist/items", new { prompt = "Q0", responseType = "pass_fail_na", isRequired = true, version }));
        version = Version(a1);
        var a2 = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/checklist/items", new { prompt = "Q1", responseType = "pass_fail_na", isRequired = true, version }));
        version = Version(a2);
        var withTeam = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/team", new { userId = users["auditor@auditx.local"], teamRole = "auditor", version }));
        version = Version(withTeam);
        var planned = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition", new { targetState = "planned", reason = (string?)null, version }));
        version = Version(planned);
        var started = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition", new { targetState = "in_progress", reason = (string?)null, version }));

        var items = started.GetProperty("checklistItems").EnumerateArray().OrderBy(i => i.GetProperty("orderIndex").GetInt32()).ToArray();
        var failItem = items[0].GetProperty("id").GetGuid();
        var passItem = items[1].GetProperty("id").GetGuid();

        await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/items/{failItem}/responses",
            new { verdict = "fail", comment = "control missing", isDraft = false, version = await AuditVersionAsync(admin, auditId) }));
        await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/items/{passItem}/responses",
            new { verdict = "pass", comment = (string?)null, isDraft = false, version = await AuditVersionAsync(admin, auditId) }));

        return (auditId, failItem, passItem, users["auditee@auditx.local"]);
    }

    [Fact]
    public async Task Raise_is_gated_to_failed_items_and_starts_open()
    {
        var admin = await AdminAsync();
        var (auditId, failItem, passItem, owner) = await SeedAuditWithResponsesAsync(admin);

        // Raising on a Pass item is rejected (US-M5-017).
        var onPass = await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/exceptions", new
        {
            checklistItemId = passItem, title = "Bad", severity = "medium", rootCause = "x", recommendation = "y", ownerUserId = owner,
        });
        Assert.Equal(HttpStatusCode.UnprocessableContent, onPass.StatusCode);

        // Raising on the Fail item succeeds and opens the exception.
        var raised = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/exceptions", new
        {
            checklistItemId = failItem, title = "Segregation gap", severity = "high",
            rootCause = "no maker-checker", recommendation = "introduce dual control", ownerUserId = owner,
        }));
        Assert.Equal("open", raised.GetProperty("status").GetString());
        Assert.Equal("high", raised.GetProperty("severity").GetString());

        // It appears in the per-audit list and the cross-audit tracker.
        var list = await DataAsync(await admin.GetAsync($"/api/v1/audits/{auditId}/exceptions"));
        Assert.Equal(1, list.GetArrayLength());
    }

    [Fact]
    public async Task A_configured_rule_lets_an_na_response_be_exception_eligible()
    {
        var admin = await AdminAsync();

        // Configure a rule allowing exceptions on N/A for pass_fail_na items (idempotent: an earlier test run
        // in the same shared DB may already have created it, in which case Create returns 409 and we edit).
        var existing = await DataAsync(await admin.GetAsync("/api/v1/exception-raising-rules"));
        var already = existing.EnumerateArray().FirstOrDefault(r => r.GetProperty("responseType").GetString() == "pass_fail_na");
        if (already.ValueKind == JsonValueKind.Undefined)
        {
            var create = await admin.PostAsJsonAsync("/api/v1/exception-raising-rules", new { responseType = "pass_fail_na", allowOnNa = true, scoreThreshold = (decimal?)null });
            create.EnsureSuccessStatusCode();
        }
        else
        {
            (await admin.PatchAsJsonAsync($"/api/v1/exception-raising-rules/{already.GetProperty("id").GetGuid()}", new { allowOnNa = true, scoreThreshold = (decimal?)null, isActive = true })).EnsureSuccessStatusCode();
        }

        // Seed an audit whose one item gets a finalised N/A response.
        var users = await UsersByEmailAsync(admin);
        var created = await DataAsync(await admin.PostAsJsonAsync("/api/v1/audits", new
        {
            name = $"NA-eligible {Guid.NewGuid():N}", auditType = "branch", startDate = "2027-01-10", targetEndDate = "2027-02-10",
            leadUserId = users["manager@auditx.local"], auditeeUserId = users["auditee@auditx.local"],
        }));
        var auditId = created.GetProperty("id").GetGuid();
        var version = Version(created);
        var withItem = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/checklist/items", new { prompt = "Q", responseType = "pass_fail_na", isRequired = true, version }));
        version = Version(withItem);
        var withTeam = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/team", new { userId = users["auditor@auditx.local"], teamRole = "auditor", version }));
        version = Version(withTeam);
        var planned = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition", new { targetState = "planned", reason = (string?)null, version }));
        version = Version(planned);
        var started = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition", new { targetState = "in_progress", reason = (string?)null, version }));
        var naItem = started.GetProperty("checklistItems").EnumerateArray().First().GetProperty("id").GetGuid();

        await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/items/{naItem}/responses",
            new { verdict = "na", comment = "Not applicable this cycle", isDraft = false, version = await AuditVersionAsync(admin, auditId) }));

        // With the rule active, an exception can now be raised on the N/A response.
        var raised = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/exceptions", new
        {
            checklistItemId = naItem, title = "N/A but risky", severity = "medium",
            rootCause = "scope excluded", recommendation = "revisit scope", ownerUserId = users["auditee@auditx.local"],
        }));
        Assert.Equal("open", raised.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Severity_is_derived_from_the_items_risk_rating_when_it_has_one()
    {
        var admin = await AdminAsync();
        var users = await UsersByEmailAsync(admin);
        var created = await DataAsync(await admin.PostAsJsonAsync("/api/v1/audits", new
        {
            name = $"Risk-rated {Guid.NewGuid():N}",
            auditType = "branch",
            startDate = "2027-01-10",
            targetEndDate = "2027-02-10",
            leadUserId = users["manager@auditx.local"],
            auditeeUserId = users["auditee@auditx.local"],
        }));
        var auditId = created.GetProperty("id").GetGuid();
        var version = Version(created);

        // Two Critical-risk-rated items — one raised without a severity, one with a (should-be-ignored) override.
        var withItem0 = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/checklist/items",
            new { prompt = "Q0", responseType = "pass_fail_na", isRequired = true, version, riskRating = "critical" }));
        version = Version(withItem0);
        var withItem1 = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/checklist/items",
            new { prompt = "Q1", responseType = "pass_fail_na", isRequired = true, version, riskRating = "critical" }));
        version = Version(withItem1);
        var withTeam = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/team", new { userId = users["auditor@auditx.local"], teamRole = "auditor", version }));
        version = Version(withTeam);
        var planned = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition", new { targetState = "planned", reason = (string?)null, version }));
        version = Version(planned);
        var started = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition", new { targetState = "in_progress", reason = (string?)null, version }));

        var items = started.GetProperty("checklistItems").EnumerateArray().OrderBy(i => i.GetProperty("orderIndex").GetInt32()).ToArray();
        var item0 = items[0].GetProperty("id").GetGuid();
        var item1 = items[1].GetProperty("id").GetGuid();
        Assert.Equal("critical", items[0].GetProperty("riskRating").GetString());

        await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/items/{item0}/responses",
            new { verdict = "fail", comment = "control missing", isDraft = false, version = await AuditVersionAsync(admin, auditId) }));
        await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/items/{item1}/responses",
            new { verdict = "fail", comment = "control missing", isDraft = false, version = await AuditVersionAsync(admin, auditId) }));

        // No severity supplied — the item's own Critical risk rating drives it.
        var raised = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/exceptions", new
        {
            checklistItemId = item0, title = "Segregation gap", rootCause = "no maker-checker",
            recommendation = "introduce dual control", ownerUserId = users["auditee@auditx.local"],
        }));
        Assert.Equal("critical", raised.GetProperty("severity").GetString());

        // A caller-supplied severity is ignored in favour of the item's risk rating.
        var raisedWithIgnoredOverride = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/exceptions", new
        {
            checklistItemId = item1, title = "Segregation gap 2", severity = "low", rootCause = "x", recommendation = "y", ownerUserId = users["auditee@auditx.local"],
        }));
        Assert.Equal("critical", raisedWithIgnoredOverride.GetProperty("severity").GetString());
    }

    [Fact]
    public async Task Raising_without_severity_on_an_unrated_item_is_rejected()
    {
        var admin = await AdminAsync();
        var (auditId, failItem, _, owner) = await SeedAuditWithResponsesAsync(admin);

        var response = await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/exceptions", new
        {
            checklistItemId = failItem, title = "No severity given", rootCause = "x", recommendation = "y", ownerUserId = owner,
        });
        Assert.Equal(HttpStatusCode.UnprocessableContent, response.StatusCode);
    }

    /// <summary>Grant the Audit Manager role (ViewAnalytics) to 'manager' and log in as an analyst.</summary>
    private async Task<HttpClient> AnalystAsync(HttpClient admin)
    {
        var users = await UsersByEmailAsync(admin);
        var roles = await DataAsync(await admin.GetAsync("/api/v1/roles"));
        var roleId = roles.EnumerateArray().First(r => r.GetProperty("name").GetString() == "Audit Manager").GetProperty("id").GetGuid();
        await admin.PostAsJsonAsync($"/api/v1/users/{users["manager@auditx.local"]}/roles", new { roleId, scopeValue = (string?)null });
        var client = NewClient();
        (await client.PostAsJsonAsync("/api/v1/auth/login", new { username = "manager", password = "Passw0rd!" })).EnsureSuccessStatusCode();
        return client;
    }

    [Fact]
    public async Task Management_response_reopen_and_verification_wiring()
    {
        var admin = await AdminAsync(); // Administrator: ManageException + ReopenException + VerifyException
        var (auditId, failItem, _, owner) = await SeedAuditWithResponsesAsync(admin);
        var ex = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/exceptions", new
        {
            checklistItemId = failItem, title = "Gap", severity = "medium", rootCause = "rc", recommendation = "rec",
            rootCauseCategory = "process_gap", ownerUserId = owner,
        }));
        var exId = ex.GetProperty("id").GetGuid();

        // Management response is recorded on the open finding.
        var responded = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/exceptions/{exId}/management-response", new
        {
            decision = "partially_accepted", comment = "We accept the control gap but dispute the severity.", version = Version(ex),
        }));
        Assert.Equal("partially_accepted", responded.GetProperty("managementResponseDecision").GetString());

        // Reopen + verification are only valid once closed → 409 on an open finding (endpoint + permission wired).
        var reopen = await admin.PostAsJsonAsync($"/api/v1/exceptions/{exId}/reopen", new
        {
            reason = "Trying to reopen a finding that is not closed yet.", version = Version(responded),
        });
        Assert.Equal(HttpStatusCode.Conflict, reopen.StatusCode);

        var verify = await admin.PostAsJsonAsync($"/api/v1/exceptions/{exId}/verifications", new
        {
            result = "passed", notes = "n/a", version = Version(responded),
        });
        Assert.Equal(HttpStatusCode.Conflict, verify.StatusCode);

        // Follow-up analytics returns its shape (ViewAnalytics).
        var analyst = await AnalystAsync(admin);
        var followup = await DataAsync(await analyst.GetAsync("/api/v1/analytics/finding-followup"));
        Assert.True(followup.GetProperty("totalFindings").GetInt32() >= 1);
        Assert.True(followup.GetProperty("withManagementResponse").GetInt32() >= 1);
    }

    [Fact]
    public async Task Response_due_date_drives_timeliness_and_sla_analytics()
    {
        var admin = await AdminAsync();
        var (auditId, failItem, _, owner) = await SeedAuditWithResponsesAsync(admin);
        var ex = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/exceptions", new
        {
            checklistItemId = failItem, title = "SLA gap", severity = "medium", rootCause = "rc", recommendation = "rec", ownerUserId = owner,
        }));
        var exId = ex.GetProperty("id").GetGuid();

        // A due date in the PAST with no response yet → timeliness is 'overdue'.
        var withDue = await DataAsync(await admin.PatchAsJsonAsync($"/api/v1/exceptions/{exId}/response-due-date", new
        {
            dueDate = "2020-01-01", version = Version(ex),
        }));
        Assert.Equal("2020-01-01", withDue.GetProperty("managementResponseDueDate").GetString());
        Assert.Equal("overdue", withDue.GetProperty("managementResponseTimeliness").GetString());

        // Recording a response now (after the past due date) flips timeliness to 'late'.
        var responded = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/exceptions/{exId}/management-response", new
        {
            decision = "accepted", comment = "Acknowledged, remediation underway.", version = Version(withDue),
        }));
        Assert.Equal("late", responded.GetProperty("managementResponseTimeliness").GetString());

        // The follow-up analytics SLA slice reflects the late response + a non-null average turnaround.
        var analyst = await AnalystAsync(admin);
        var followup = await DataAsync(await analyst.GetAsync("/api/v1/analytics/finding-followup"));
        Assert.True(followup.GetProperty("withResponseDue").GetInt32() >= 1);
        Assert.True(followup.GetProperty("respondedLate").GetInt32() >= 1);
        Assert.NotEqual(JsonValueKind.Null, followup.GetProperty("averageResponseDays").ValueKind);

        // Clearing the due date removes the timeliness signal.
        var cleared = await DataAsync(await admin.PatchAsJsonAsync($"/api/v1/exceptions/{exId}/response-due-date", new
        {
            dueDate = (string?)null, version = Version(responded),
        }));
        Assert.Equal(JsonValueKind.Null, cleared.GetProperty("managementResponseDueDate").ValueKind);
        Assert.Equal(JsonValueKind.Null, cleared.GetProperty("managementResponseTimeliness").ValueKind);
    }

    [Fact]
    public async Task Setting_the_response_due_date_requires_manage_exception()
    {
        var admin = await AdminAsync();
        var (auditId, failItem, _, owner) = await SeedAuditWithResponsesAsync(admin);
        var ex = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/exceptions", new
        {
            checklistItemId = failItem, title = "Gap", severity = "low", rootCause = "rc", recommendation = "rec", ownerUserId = owner,
        }));

        var auditee = NewClient();
        (await auditee.PostAsJsonAsync("/api/v1/auth/login", new { username = "auditee", password = "Passw0rd!" })).EnsureSuccessStatusCode();
        var denied = await auditee.PatchAsJsonAsync($"/api/v1/exceptions/{ex.GetProperty("id").GetGuid()}/response-due-date", new
        {
            dueDate = "2027-01-01", version = Version(ex),
        });
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task Reopen_requires_the_reopen_permission()
    {
        var admin = await AdminAsync();
        var (auditId, failItem, _, owner) = await SeedAuditWithResponsesAsync(admin);
        var ex = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/exceptions", new
        {
            checklistItemId = failItem, title = "Gap", severity = "low", rootCause = "rc", recommendation = "rec",
            rootCauseCategory = "human_error", ownerUserId = owner,
        }));

        var auditee = NewClient();
        (await auditee.PostAsJsonAsync("/api/v1/auth/login", new { username = "auditee", password = "Passw0rd!" })).EnsureSuccessStatusCode();
        var resp = await auditee.PostAsJsonAsync($"/api/v1/exceptions/{ex.GetProperty("id").GetGuid()}/reopen", new
        {
            reason = "Auditee should not be able to reopen a finding at all.", version = Version(ex),
        });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Raise_captures_the_root_cause_taxonomy()
    {
        var admin = await AdminAsync();
        var (auditId, failItem, _, owner) = await SeedAuditWithResponsesAsync(admin);

        var raised = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/exceptions", new
        {
            checklistItemId = failItem, title = "Segregation gap", severity = "high",
            rootCause = "no maker-checker", recommendation = "introduce dual control",
            category = "operational", rootCauseCategory = "process_gap", ownerUserId = owner,
        }));
        Assert.Equal("process_gap", raised.GetProperty("rootCauseCategory").GetString());

        // It survives a re-read of the detail.
        var reread = await DataAsync(await admin.GetAsync($"/api/v1/exceptions/{raised.GetProperty("id").GetGuid()}"));
        Assert.Equal("process_gap", reread.GetProperty("rootCauseCategory").GetString());
    }

    [Fact]
    public async Task Raise_captures_the_non_conformance_taxonomy_and_surfaces_it_in_the_portfolio()
    {
        var admin = await AdminAsync();
        var (auditId, failItem, _, owner) = await SeedAuditWithResponsesAsync(admin);

        var raised = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/exceptions", new
        {
            checklistItemId = failItem, title = "Regulatory breach", severity = "high",
            rootCause = "no maker-checker", recommendation = "introduce dual control",
            category = "regulatory", rootCauseCategory = "process_gap", nonConformanceCategory = "regulatory_breach",
            ownerUserId = owner,
        }));
        Assert.Equal("regulatory_breach", raised.GetProperty("nonConformanceCategory").GetString());

        // Survives a re-read of the detail.
        var reread = await DataAsync(await admin.GetAsync($"/api/v1/exceptions/{raised.GetProperty("id").GetGuid()}"));
        Assert.Equal("regulatory_breach", reread.GetProperty("nonConformanceCategory").GetString());

        // The open finding is grouped by non-conformance in the exception-portfolio analytics.
        var analyst = await AnalystAsync(admin);
        var portfolio = await DataAsync(await analyst.GetAsync("/api/v1/analytics/exception-portfolio"));
        Assert.Contains(
            portfolio.GetProperty("byNonConformance").EnumerateArray(),
            n => n.GetProperty("nonConformanceCategory").GetString() == "regulatory_breach");

        // Drilldown: the tracker filters by the taxonomy codes (the dashboard chart click-through path).
        var id = raised.GetProperty("id").GetGuid();
        var byNc = await DataAsync(await admin.GetAsync("/api/v1/exceptions?nonConformanceCategory=regulatory_breach"));
        Assert.Contains(byNc.GetProperty("items").EnumerateArray(), e => e.GetProperty("id").GetGuid() == id);

        var byRc = await DataAsync(await admin.GetAsync("/api/v1/exceptions?rootCauseCategory=process_gap"));
        Assert.Contains(byRc.GetProperty("items").EnumerateArray(), e => e.GetProperty("id").GetGuid() == id);

        // A different code excludes it.
        var other = await DataAsync(await admin.GetAsync("/api/v1/exceptions?nonConformanceCategory=data_quality"));
        Assert.DoesNotContain(other.GetProperty("items").EnumerateArray(), e => e.GetProperty("id").GetGuid() == id);

        // The 'open_any' umbrella (drilldowns carry it so the list matches the open-findings KPIs) includes it too.
        var openAny = await DataAsync(await admin.GetAsync("/api/v1/exceptions?status=open_any&nonConformanceCategory=regulatory_breach"));
        Assert.Contains(openAny.GetProperty("items").EnumerateArray(), e => e.GetProperty("id").GetGuid() == id);
    }

    [Fact]
    public async Task Map_submit_then_gated_approve_returns_pending_action()
    {
        var admin = await AdminAsync();
        var (auditId, failItem, _, owner) = await SeedAuditWithResponsesAsync(admin);

        var ex = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/exceptions", new
        {
            checklistItemId = failItem, title = "Gap", severity = "medium", rootCause = "rc", recommendation = "rec", ownerUserId = owner,
        }));
        var exId = ex.GetProperty("id").GetGuid();
        var version = Version(ex);

        var submitted = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/exceptions/{exId}/map", new
        {
            // Must be on/before the severity-derived exception target date (medium → today + 45 days).
            actions = new[] { new { description = "Implement dual control", ownerUserId = owner, targetDate = "2026-07-01", expectedEvidenceType = "screenshot" } },
            version,
        }));
        Assert.Equal("map_submitted", submitted.GetProperty("status").GetString());
        version = Version(submitted);

        // map_approval is maker-checker-gated by default → 202 with a pending action id (not executed).
        var approve = await admin.PostAsJsonAsync($"/api/v1/exceptions/{exId}/map/approve", new { version });
        Assert.Equal(HttpStatusCode.Accepted, approve.StatusCode);

        // The MAP is still only submitted (approval captured, not applied).
        var after = await DataAsync(await admin.GetAsync($"/api/v1/exceptions/{exId}"));
        Assert.Equal("map_submitted", after.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Stale_version_on_severity_change_conflicts()
    {
        var admin = await AdminAsync();
        var (auditId, failItem, _, owner) = await SeedAuditWithResponsesAsync(admin);

        var ex = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/exceptions", new
        {
            checklistItemId = failItem, title = "Gap", severity = "low", rootCause = "rc", recommendation = "rec", ownerUserId = owner,
        }));
        var exId = ex.GetProperty("id").GetGuid();
        var stale = Version(ex);

        await DataAsync(await admin.PatchAsJsonAsync($"/api/v1/exceptions/{exId}/severity",
            new { severity = "high", reason = "Escalated after manager review of the impact.", version = stale }));

        var conflict = await admin.PatchAsJsonAsync($"/api/v1/exceptions/{exId}/severity",
            new { severity = "critical", reason = "Escalated again using a stale version token.", version = stale });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
    }
}
