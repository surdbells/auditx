using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AuditX.Api.IntegrationTests;

/// <summary>
/// P1-B controls + compliance registers: control register/effectiveness lifecycle, duplicate-code guard,
/// regulation register, finding↔control/regulation links (access-scoped, idempotent) and the
/// control-effectiveness + compliance-by-regulation analytics.
/// </summary>
public sealed class ControlComplianceFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
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

    private static string Version(JsonElement node) => node.GetProperty("version").GetString()!;

    private static async Task<Dictionary<string, Guid>> UsersByEmailAsync(HttpClient admin)
    {
        var data = await DataAsync(await admin.GetAsync("/api/v1/users?limit=100"));
        return data.GetProperty("items").EnumerateArray()
            .ToDictionary(u => u.GetProperty("email").GetString()!, u => u.GetProperty("id").GetGuid());
    }

    /// <summary>Grant the Audit Manager role (ViewAnalytics) to 'manager' and log in.</summary>
    private async Task<HttpClient> AnalystAsync(HttpClient admin)
    {
        var users = await UsersByEmailAsync(admin);
        var roles = await DataAsync(await admin.GetAsync("/api/v1/roles"));
        var roleId = roles.EnumerateArray().First(r => r.GetProperty("name").GetString() == "Audit Manager").GetProperty("id").GetGuid();
        await admin.PostAsJsonAsync($"/api/v1/users/{users["manager@auditx.local"]}/roles", new { roleId, scopeValue = (string?)null });
        return await LoginAsync("manager");
    }

    private async Task<JsonElement> RegisterControlAsync(HttpClient admin, Guid owner, string? code = null)
        => await DataAsync(await admin.PostAsJsonAsync("/api/v1/controls", new
        {
            code = code ?? $"CTL-{Guid.NewGuid().ToString("N")[..8]}",
            title = "Dual authorisation on wire transfers",
            description = "Two-person approval for payments above the threshold.",
            controlType = "preventive",
            frequency = "continuous",
            ownerUserId = owner,
        }));

    private static async Task<string> AuditVersionAsync(HttpClient admin, Guid auditId)
        => Version(await DataAsync(await admin.GetAsync($"/api/v1/audits/{auditId}")));

    /// <summary>Build an in-progress audit with a failed item and raise an OPEN finding on it; returns the finding id.</summary>
    private async Task<Guid> SeedOpenFindingAsync(HttpClient admin)
    {
        var users = await UsersByEmailAsync(admin);
        var created = await DataAsync(await admin.PostAsJsonAsync("/api/v1/audits", new
        {
            name = $"Ctl {Guid.NewGuid():N}",
            auditType = "branch",
            startDate = "2027-03-10",
            targetEndDate = "2027-04-10",
            leadUserId = users["manager@auditx.local"],
            auditeeUserId = users["auditee@auditx.local"],
        }));
        var auditId = created.GetProperty("id").GetGuid();
        var version = Version(created);

        var a1 = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/checklist/items", new { prompt = "Q0", responseType = "pass_fail_na", isRequired = true, version }));
        version = Version(a1);
        var withTeam = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/team", new { userId = users["auditor@auditx.local"], teamRole = "auditor", version }));
        version = Version(withTeam);
        var planned = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition", new { targetState = "planned", reason = (string?)null, version }));
        version = Version(planned);
        var started = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition", new { targetState = "in_progress", reason = (string?)null, version }));

        var failItem = started.GetProperty("checklistItems").EnumerateArray().First().GetProperty("id").GetGuid();
        await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/items/{failItem}/responses",
            new { verdict = "fail", comment = "control missing", isDraft = false, version = await AuditVersionAsync(admin, auditId) }));

        var raised = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/exceptions", new
        {
            checklistItemId = failItem, title = "Segregation gap", severity = "high",
            rootCause = "no maker-checker", recommendation = "introduce dual control", ownerUserId = users["auditee@auditx.local"],
        }));
        return raised.GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Register_then_test_effectiveness_lifecycle()
    {
        var admin = await LoginAsync("admin"); // Administrator: View + Manage controls
        var users = await UsersByEmailAsync(admin);

        var control = await RegisterControlAsync(admin, users["manager@auditx.local"]);
        var controlId = control.GetProperty("id").GetGuid();
        Assert.Equal("not_tested", control.GetProperty("effectiveness").GetString());
        Assert.True(control.GetProperty("isActive").GetBoolean());
        Assert.Equal("preventive", control.GetProperty("controlType").GetString());

        // Record a test result → effectiveness + last-tested date captured.
        var tested = await DataAsync(await admin.PatchAsJsonAsync($"/api/v1/controls/{controlId}", new
        {
            title = control.GetProperty("title").GetString(),
            description = "Two-person approval for payments above the threshold.",
            controlType = "preventive",
            frequency = "continuous",
            ownerUserId = users["manager@auditx.local"],
            auditableEntityId = (Guid?)null,
            effectiveness = "partially_effective",
            lastTestedDate = "2026-06-30",
            version = Version(control),
        }));
        Assert.Equal("partially_effective", tested.GetProperty("effectiveness").GetString());
        Assert.Equal("2026-06-30", tested.GetProperty("lastTestedDate").GetString());

        // It appears in the register.
        var list = await DataAsync(await admin.GetAsync("/api/v1/controls?limit=100"));
        Assert.Contains(list.GetProperty("items").EnumerateArray(), c => c.GetProperty("id").GetGuid() == controlId);
    }

    [Fact]
    public async Task Effectiveness_result_requires_a_last_tested_date()
    {
        var admin = await LoginAsync("admin");
        var users = await UsersByEmailAsync(admin);
        var control = await RegisterControlAsync(admin, users["manager@auditx.local"]);

        // effectiveness != not_tested but no lastTestedDate → domain rule rejects (422).
        var bad = await admin.PatchAsJsonAsync($"/api/v1/controls/{control.GetProperty("id").GetGuid()}", new
        {
            title = "x", description = (string?)null, controlType = "detective", frequency = "monthly",
            ownerUserId = users["manager@auditx.local"], auditableEntityId = (Guid?)null,
            effectiveness = "effective", lastTestedDate = (string?)null, version = Version(control),
        });
        Assert.Equal(HttpStatusCode.UnprocessableContent, bad.StatusCode);
    }

    [Fact]
    public async Task Duplicate_control_code_conflicts()
    {
        var admin = await LoginAsync("admin");
        var users = await UsersByEmailAsync(admin);
        var code = $"CTL-{Guid.NewGuid().ToString("N")[..8]}";
        await RegisterControlAsync(admin, users["manager@auditx.local"], code);

        var dup = await admin.PostAsJsonAsync("/api/v1/controls", new
        {
            code, title = "Another", description = (string?)null, controlType = "detective",
            frequency = "monthly", ownerUserId = users["manager@auditx.local"],
        });
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);
    }

    [Fact]
    public async Task Managing_controls_requires_the_manage_controls_permission()
    {
        var admin = await LoginAsync("admin");
        var users = await UsersByEmailAsync(admin);

        var auditee = await LoginAsync("auditee"); // no ManageControls
        var resp = await auditee.PostAsJsonAsync("/api/v1/controls", new
        {
            code = "CTL-NOPE", title = "Nope", description = (string?)null, controlType = "preventive",
            frequency = "continuous", ownerUserId = users["manager@auditx.local"],
        });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Register_regulation_and_duplicate_code_conflicts()
    {
        var admin = await LoginAsync("admin");
        var code = $"REG-{Guid.NewGuid().ToString("N")[..8]}";

        var reg = await DataAsync(await admin.PostAsJsonAsync("/api/v1/regulations", new
        {
            code, name = "AML/CFT Regulations 2022", authority = "Central Bank", description = "Anti-money-laundering rules.", category = "AML",
        }));
        Assert.Equal(code, reg.GetProperty("code").GetString());
        Assert.True(reg.GetProperty("isActive").GetBoolean());

        var dup = await admin.PostAsJsonAsync("/api/v1/regulations", new
        {
            code, name = "Clash", authority = (string?)null, description = (string?)null, category = (string?)null,
        });
        Assert.Equal(HttpStatusCode.Conflict, dup.StatusCode);

        // Over-length Authority is rejected by validation (422), not a DB-truncation 500.
        var tooLong = await admin.PostAsJsonAsync("/api/v1/regulations", new
        {
            code = $"REG-{Guid.NewGuid().ToString("N")[..8]}", name = "Long authority",
            authority = new string('A', 201), description = (string?)null, category = (string?)null,
        });
        Assert.Equal(HttpStatusCode.UnprocessableContent, tooLong.StatusCode);
    }

    [Fact]
    public async Task Link_control_and_regulation_to_a_finding_then_unlink()
    {
        var admin = await LoginAsync("admin"); // ManageException + Manage controls
        var users = await UsersByEmailAsync(admin);
        var findingId = await SeedOpenFindingAsync(admin);

        var control = await RegisterControlAsync(admin, users["manager@auditx.local"]);
        var controlId = control.GetProperty("id").GetGuid();
        var reg = await DataAsync(await admin.PostAsJsonAsync("/api/v1/regulations", new
        {
            code = $"REG-{Guid.NewGuid().ToString("N")[..8]}", name = "Ops Risk Directive", authority = "Regulator",
            description = (string?)null, category = "Operational",
        }));
        var regId = reg.GetProperty("id").GetGuid();

        // Link both.
        await DataAsync(await admin.PostAsJsonAsync($"/api/v1/exceptions/{findingId}/controls", new { controlId }));
        await DataAsync(await admin.PostAsJsonAsync($"/api/v1/exceptions/{findingId}/regulations", new { regulationId = regId }));

        // Re-linking the same control is idempotent (returns the existing link, still 2xx).
        (await admin.PostAsJsonAsync($"/api/v1/exceptions/{findingId}/controls", new { controlId })).EnsureSuccessStatusCode();

        var links = await DataAsync(await admin.GetAsync($"/api/v1/exceptions/{findingId}/links"));
        Assert.Equal(1, links.GetProperty("controls").GetArrayLength());
        Assert.Equal(1, links.GetProperty("regulations").GetArrayLength());
        Assert.Equal(controlId, links.GetProperty("controls")[0].GetProperty("controlId").GetGuid());
        Assert.Equal(regId, links.GetProperty("regulations")[0].GetProperty("regulationId").GetGuid());

        // Unlink the control (idempotent — a second delete still succeeds).
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/exceptions/{findingId}/controls/{controlId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.DeleteAsync($"/api/v1/exceptions/{findingId}/controls/{controlId}")).StatusCode);

        var after = await DataAsync(await admin.GetAsync($"/api/v1/exceptions/{findingId}/links"));
        Assert.Equal(0, after.GetProperty("controls").GetArrayLength());
        Assert.Equal(1, after.GetProperty("regulations").GetArrayLength());
    }

    [Fact]
    public async Task Analytics_reflect_control_effectiveness_and_regulation_compliance()
    {
        var admin = await LoginAsync("admin");
        var analyst = await AnalystAsync(admin); // ViewAnalytics
        var users = await UsersByEmailAsync(admin);

        // An ineffective active control.
        var control = await RegisterControlAsync(admin, users["manager@auditx.local"]);
        await DataAsync(await admin.PatchAsJsonAsync($"/api/v1/controls/{control.GetProperty("id").GetGuid()}", new
        {
            title = control.GetProperty("title").GetString(), description = (string?)null,
            controlType = "preventive", frequency = "continuous", ownerUserId = users["manager@auditx.local"],
            auditableEntityId = (Guid?)null, effectiveness = "ineffective", lastTestedDate = "2026-06-15", version = Version(control),
        }));

        var effectiveness = await DataAsync(await analyst.GetAsync("/api/v1/analytics/control-effectiveness"));
        Assert.True(effectiveness.GetProperty("totalActive").GetInt32() >= 1);
        Assert.True(effectiveness.GetProperty("ineffective").GetInt32() >= 1);
        Assert.Contains(effectiveness.GetProperty("byType").EnumerateArray(), t => t.GetProperty("key").GetString() == "preventive");

        // A regulation linked to an open finding shows up in compliance-by-regulation.
        var findingId = await SeedOpenFindingAsync(admin);
        var reg = await DataAsync(await admin.PostAsJsonAsync("/api/v1/regulations", new
        {
            code = $"REG-{Guid.NewGuid().ToString("N")[..8]}", name = "Capital Adequacy", authority = "Central Bank",
            description = (string?)null, category = "Prudential",
        }));
        var regId = reg.GetProperty("id").GetGuid();
        await DataAsync(await admin.PostAsJsonAsync($"/api/v1/exceptions/{findingId}/regulations", new { regulationId = regId }));

        var compliance = await DataAsync(await analyst.GetAsync("/api/v1/analytics/compliance-by-regulation"));
        var row = compliance.EnumerateArray().First(r => r.GetProperty("regulationId").GetGuid() == regId);
        Assert.True(row.GetProperty("linkedFindings").GetInt32() >= 1);
        Assert.True(row.GetProperty("openFindings").GetInt32() >= 1);
    }

    [Fact]
    public async Task Control_risk_link_is_idempotent_listable_and_removable()
    {
        var admin = await LoginAsync("admin");
        var users = await UsersByEmailAsync(admin);
        var owner = users["manager@auditx.local"];

        var control = await RegisterControlAsync(admin, owner);
        var controlId = control.GetProperty("id").GetGuid();

        var risk = await DataAsync(await admin.PostAsJsonAsync("/api/v1/risks", new
        {
            title = $"Wire-transfer fraud {Guid.NewGuid():N}", description = "Unauthorised outbound payments",
            category = "Operational", ownerUserId = owner, inherentLikelihood = 4, inherentImpact = 5,
        }));
        var riskId = risk.GetProperty("id").GetGuid();

        // Link, then link again — idempotent (same link row, no duplicate, no error).
        var linked = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/controls/{controlId}/risks", new { riskId }));
        var firstLinkId = linked.GetProperty("linkId").GetGuid();
        var relinked = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/controls/{controlId}/risks", new { riskId }));
        Assert.Equal(firstLinkId, relinked.GetProperty("linkId").GetGuid());

        // It shows up in the control's linked-risks list with the risk's title + status.
        var list = await DataAsync(await admin.GetAsync($"/api/v1/controls/{controlId}/risks"));
        var linkRow = list.EnumerateArray().Single();
        Assert.Equal(riskId, linkRow.GetProperty("riskId").GetGuid());
        Assert.Equal("open", linkRow.GetProperty("status").GetString());

        // Unlink removes it.
        (await admin.DeleteAsync($"/api/v1/controls/{controlId}/risks/{riskId}")).EnsureSuccessStatusCode();
        var afterUnlink = await DataAsync(await admin.GetAsync($"/api/v1/controls/{controlId}/risks"));
        Assert.Equal(0, afterUnlink.GetArrayLength());
    }

    [Fact]
    public async Task Linking_an_unknown_risk_to_a_control_is_rejected()
    {
        var admin = await LoginAsync("admin");
        var owner = (await UsersByEmailAsync(admin))["manager@auditx.local"];
        var control = await RegisterControlAsync(admin, owner);
        var controlId = control.GetProperty("id").GetGuid();

        var response = await admin.PostAsJsonAsync($"/api/v1/controls/{controlId}/risks", new { riskId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Recording_a_test_from_a_failed_item_marks_the_control_ineffective_and_logs_history()
    {
        var admin = await LoginAsync("admin");
        var users = await UsersByEmailAsync(admin);
        var owner = users["manager@auditx.local"];

        var control = await RegisterControlAsync(admin, owner);
        var controlId = control.GetProperty("id").GetGuid();
        Assert.Equal("not_tested", control.GetProperty("effectiveness").GetString());

        // Audit whose item tests this control, responded Fail.
        var created = await DataAsync(await admin.PostAsJsonAsync("/api/v1/audits", new
        {
            name = $"Ctrl-test {Guid.NewGuid():N}", auditType = "branch", startDate = "2027-01-10", targetEndDate = "2027-02-10",
            leadUserId = owner, auditeeUserId = users["auditee@auditx.local"],
        }));
        var auditId = created.GetProperty("id").GetGuid();
        var version = Version(created);
        var withItem = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/checklist/items",
            new { prompt = "Is the control operating?", responseType = "pass_fail_na", isRequired = true, version, controlId }));
        version = Version(withItem);
        var withTeam = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/team", new { userId = users["auditor@auditx.local"], teamRole = "auditor", version }));
        version = Version(withTeam);
        var planned = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition", new { targetState = "planned", reason = (string?)null, version }));
        version = Version(planned);
        var started = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition", new { targetState = "in_progress", reason = (string?)null, version }));
        var itemId = started.GetProperty("checklistItems")[0].GetProperty("id").GetGuid();

        await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/items/{itemId}/responses",
            new { verdict = "fail", comment = "control not operating", isDraft = false, version = await AuditVersionAsync(admin, auditId) }));

        // Record the test with no explicit result — derived from the Fail response → ineffective.
        var test = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/controls/{controlId}/tests",
            new { auditId, checklistItemId = itemId, result = (string?)null, notes = "Observed during fieldwork" }));
        Assert.Equal("ineffective", test.GetProperty("result").GetString());

        // The control's current effectiveness now reflects the test, with a last-tested date.
        var refreshed = await DataAsync(await admin.GetAsync($"/api/v1/controls/{controlId}"));
        Assert.Equal("ineffective", refreshed.GetProperty("effectiveness").GetString());
        Assert.False(string.IsNullOrEmpty(refreshed.GetProperty("lastTestedDate").GetString()));

        // The test appears in the append-only history.
        var history = await DataAsync(await admin.GetAsync($"/api/v1/controls/{controlId}/tests"));
        Assert.Equal(1, history.GetArrayLength());
        Assert.Equal(itemId, history[0].GetProperty("checklistItemId").GetGuid());
    }

    [Fact]
    public async Task Recording_a_test_for_an_item_that_tests_a_different_control_is_rejected()
    {
        var admin = await LoginAsync("admin");
        var users = await UsersByEmailAsync(admin);
        var owner = users["manager@auditx.local"];

        var testedControl = await RegisterControlAsync(admin, owner);
        var otherControl = await RegisterControlAsync(admin, owner);
        var testedControlId = testedControl.GetProperty("id").GetGuid();
        var otherControlId = otherControl.GetProperty("id").GetGuid();

        var created = await DataAsync(await admin.PostAsJsonAsync("/api/v1/audits", new
        {
            name = $"Ctrl-mismatch {Guid.NewGuid():N}", auditType = "branch", startDate = "2027-01-10", targetEndDate = "2027-02-10",
            leadUserId = owner, auditeeUserId = users["auditee@auditx.local"],
        }));
        var auditId = created.GetProperty("id").GetGuid();
        var version = Version(created);
        var withItem = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/checklist/items",
            new { prompt = "Q", responseType = "pass_fail_na", isRequired = true, version, controlId = testedControlId }));
        var itemId = withItem.GetProperty("checklistItems")[0].GetProperty("id").GetGuid();

        // Try to record a test for OTHER control from an item that tests TESTED control → rejected.
        var response = await admin.PostAsJsonAsync($"/api/v1/controls/{otherControlId}/tests",
            new { auditId, checklistItemId = itemId, result = "effective", notes = (string?)null });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}
