using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace AuditX.Api.IntegrationTests;

public sealed class AuditExecutionFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
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

    private async Task<string> VersionAsync(HttpClient admin, Guid auditId)
        => Version(await DataAsync(await admin.GetAsync($"/api/v1/audits/{auditId}")));

    private static async Task<Dictionary<string, Guid>> UsersByEmailAsync(HttpClient admin)
    {
        var data = await DataAsync(await admin.GetAsync("/api/v1/users?limit=100"));
        return data.GetProperty("items").EnumerateArray()
            .ToDictionary(u => u.GetProperty("email").GetString()!, u => u.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Execution_flow_respond_evidence_and_auto_transition()
    {
        var admin = await AdminAsync();
        var users = await UsersByEmailAsync(admin);

        // Create a blank audit, add two items + an auditor, then plan → start (threading the rowversion).
        var created = await DataAsync(await admin.PostAsJsonAsync("/api/v1/audits", new
        {
            name = $"Execution {Guid.NewGuid():N}",
            auditType = "branch",
            startDate = "2027-01-10",
            targetEndDate = "2027-02-10",
            leadUserId = users["manager@auditx.local"],
            auditeeUserId = users["auditee@auditx.local"],
        }));
        var auditId = created.GetProperty("id").GetGuid();
        var version = Version(created);

        var a1 = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/checklist/items",
            new { prompt = "Cash counted?", responseType = "pass_fail_na", isRequired = true, version }));
        version = Version(a1);
        var a2 = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/checklist/items",
            new { prompt = "Logs reviewed?", responseType = "pass_fail_na", isRequired = true, version }));
        version = Version(a2);
        var withTeam = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/team",
            new { userId = users["auditor@auditx.local"], teamRole = "auditor", version }));
        version = Version(withTeam);
        var planned = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition", new { targetState = "planned", reason = (string?)null, version }));
        version = Version(planned);
        var started = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition", new { targetState = "in_progress", reason = (string?)null, version }));

        var items = started.GetProperty("checklistItems").EnumerateArray().OrderBy(i => i.GetProperty("orderIndex").GetInt32()).ToArray();
        var item1 = items[0].GetProperty("id").GetGuid();
        var item2 = items[1].GetProperty("id").GetGuid();

        // Fail without a comment → 422.
        var failNoComment = await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/items/{item1}/responses",
            new { verdict = "fail", comment = (string?)null, isDraft = false, version = await VersionAsync(admin, auditId) });
        Assert.Equal(HttpStatusCode.UnprocessableContent, failNoComment.StatusCode);

        // Draft response → item in_progress; capture the response id for evidence.
        var draft = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/items/{item1}/responses",
            new { verdict = (string?)null, comment = "working on it", isDraft = true, version = await VersionAsync(admin, auditId) }));
        var responseId = draft.GetProperty("id").GetGuid();
        Assert.True(draft.GetProperty("isDraft").GetBoolean());

        // Upload a (valid %PDF) evidence file while the audit is in progress.
        var pdf = Encoding.ASCII.GetBytes("%PDF-1.4\n%AuditX evidence sample\n");
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(pdf);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(fileContent, "file", "evidence.pdf");
        var uploaded = await DataAsync(await admin.PostAsync($"/api/v1/audits/{auditId}/responses/{responseId}/evidence", form));
        var evidenceId = uploaded.GetProperty("id").GetGuid();
        Assert.Equal(pdf.Length, uploaded.GetProperty("sizeBytes").GetInt64());

        // Download verifies the hash and returns the bytes.
        var download = await admin.GetAsync($"/api/v1/audits/{auditId}/evidence/{evidenceId}");
        download.EnsureSuccessStatusCode();
        Assert.Equal(pdf.Length, (await download.Content.ReadAsByteArrayAsync()).Length);

        // Finalise both items; the last finalisation auto-transitions the audit to Under Review.
        await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/items/{item1}/responses",
            new { verdict = "pass", comment = (string?)null, isDraft = false, version = await VersionAsync(admin, auditId) }));
        await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/items/{item2}/responses",
            new { verdict = "fail", comment = "missing log entries for week 3", isDraft = false, version = await VersionAsync(admin, auditId) }));

        var afterReview = await DataAsync(await admin.GetAsync($"/api/v1/audits/{auditId}"));
        Assert.Equal("under_review", afterReview.GetProperty("status").GetString());

        // The failed, un-justified item surfaces in the manager review list.
        var failList = await DataAsync(await admin.GetAsync($"/api/v1/audits/{auditId}/review/fail-without-exception"));
        Assert.Equal(1, failList.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task Response_captures_observation_and_recommendation_and_shows_them_in_history()
    {
        var admin = await AdminAsync();
        var users = await UsersByEmailAsync(admin);

        var created = await DataAsync(await admin.PostAsJsonAsync("/api/v1/audits", new
        {
            name = $"ObsRec {Guid.NewGuid():N}",
            auditType = "branch",
            startDate = "2027-01-10",
            targetEndDate = "2027-02-10",
            leadUserId = users["manager@auditx.local"],
            auditeeUserId = users["auditee@auditx.local"],
        }));
        var auditId = created.GetProperty("id").GetGuid();
        var version = Version(created);

        var a1 = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/checklist/items",
            new { prompt = "Segregation of duties enforced?", responseType = "pass_fail_na", isRequired = true, version }));
        version = Version(a1);
        var withTeam = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/team",
            new { userId = users["auditor@auditx.local"], teamRole = "auditor", version }));
        version = Version(withTeam);
        var planned = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition", new { targetState = "planned", reason = (string?)null, version }));
        version = Version(planned);
        var started = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition", new { targetState = "in_progress", reason = (string?)null, version }));
        var itemId = started.GetProperty("checklistItems")[0].GetProperty("id").GetGuid();

        // Finalise a Fail with an observation + recommendation.
        var response = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/items/{itemId}/responses", new
        {
            verdict = "fail",
            comment = "duties are combined",
            observation = "The same clerk both posts and approves journal entries.",
            recommendation = "Split posting and approval between two staff members.",
            isDraft = false,
            version = await VersionAsync(admin, auditId),
        }));
        Assert.Equal("The same clerk both posts and approves journal entries.", response.GetProperty("observation").GetString());
        Assert.Equal("Split posting and approval between two staff members.", response.GetProperty("recommendation").GetString());

        // The GET returns them, and the history timeline records them in the after-snapshot.
        var fetched = await DataAsync(await admin.GetAsync($"/api/v1/audits/{auditId}/items/{itemId}/responses"));
        Assert.Equal("The same clerk both posts and approves journal entries.", fetched.GetProperty("observation").GetString());

        var history = await DataAsync(await admin.GetAsync($"/api/v1/audits/{auditId}/items/{itemId}/responses/history"));
        var latest = history.EnumerateArray().Last();
        using var state = JsonDocument.Parse(latest.GetProperty("stateJson").GetString()!);
        Assert.Equal("The same clerk both posts and approves journal entries.", state.RootElement.GetProperty("observation").GetString());
        Assert.Equal("Split posting and approval between two staff members.", state.RootElement.GetProperty("recommendation").GetString());
    }

    [Fact]
    public async Task Custom_conclusion_option_drives_verdict_score_and_label()
    {
        var admin = await AdminAsync();
        var users = await UsersByEmailAsync(admin);

        // The organisation defines its own conclusion options for pass_fail_na.
        const string optionsJson = """
            [
              {"code":"compliant","label":"Compliant","order":0,"score":100,"isDeficiency":false,"isNotApplicable":false,"requiresComment":false},
              {"code":"partial","label":"Partially Compliant","order":1,"score":50,"isDeficiency":true,"isNotApplicable":false,"requiresComment":true},
              {"code":"noncompliant","label":"Non-Compliant","order":2,"score":0,"isDeficiency":true,"isNotApplicable":false,"requiresComment":true},
              {"code":"na","label":"Not Applicable","order":3,"score":null,"isDeficiency":false,"isNotApplicable":true,"requiresComment":true}
            ]
            """;
        var set = await DataAsync(await admin.PutAsJsonAsync("/api/v1/response-option-sets/pass_fail_na", new { optionsJson }));
        Assert.True(set.GetProperty("isCustomised").GetBoolean());
        Assert.Equal(4, set.GetProperty("options").GetArrayLength());

        var created = await DataAsync(await admin.PostAsJsonAsync("/api/v1/audits", new
        {
            name = $"Options {Guid.NewGuid():N}", auditType = "branch", startDate = "2027-01-10", targetEndDate = "2027-02-10",
            leadUserId = users["manager@auditx.local"], auditeeUserId = users["auditee@auditx.local"],
        }));
        var auditId = created.GetProperty("id").GetGuid();
        var version = Version(created);
        var a1 = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/checklist/items",
            new { prompt = "Reconciliations performed?", responseType = "pass_fail_na", isRequired = true, version }));
        version = Version(a1);
        var withTeam = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/team",
            new { userId = users["auditor@auditx.local"], teamRole = "auditor", version }));
        version = Version(withTeam);
        var planned = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition", new { targetState = "planned", reason = (string?)null, version }));
        version = Version(planned);
        var started = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition", new { targetState = "in_progress", reason = (string?)null, version }));
        var itemId = started.GetProperty("checklistItems")[0].GetProperty("id").GetGuid();

        // Choosing "Partially Compliant" (a finding, score 50) derives Fail, scores 50, and snapshots the label.
        var response = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/items/{itemId}/responses", new
        {
            selectedOptionCode = "partial", comment = "Two of five accounts reconciled", isDraft = false, version = await VersionAsync(admin, auditId),
        }));
        Assert.Equal("fail", response.GetProperty("verdict").GetString());
        Assert.Equal(50, response.GetProperty("score").GetInt32());
        Assert.Equal("partial", response.GetProperty("selectedOptionCode").GetString());
        Assert.Equal("Partially Compliant", response.GetProperty("selectedOptionLabel").GetString());

        // Reset restores the built-in labels.
        var reset = await DataAsync(await admin.PostAsync("/api/v1/response-option-sets/pass_fail_na/reset", null));
        Assert.False(reset.GetProperty("isCustomised").GetBoolean());
        Assert.Equal("Pass", reset.GetProperty("options").EnumerateArray().First().GetProperty("label").GetString());
    }

    [Fact]
    public async Task Evidence_upload_rejects_disallowed_mime()
    {
        var admin = await AdminAsync();
        var users = await UsersByEmailAsync(admin);

        var created = await DataAsync(await admin.PostAsJsonAsync("/api/v1/audits", new
        {
            name = $"Mime {Guid.NewGuid():N}",
            auditType = "process",
            startDate = "2027-01-10",
            targetEndDate = "2027-02-10",
            leadUserId = users["manager@auditx.local"],
            auditeeUserId = users["auditee@auditx.local"],
        }));
        var auditId = created.GetProperty("id").GetGuid();
        var version = Version(created);

        var a1 = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/checklist/items",
            new { prompt = "Q", responseType = "pass_fail_na", isRequired = true, version }));
        version = Version(a1);
        var withTeam = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/team",
            new { userId = users["auditor@auditx.local"], teamRole = "auditor", version }));
        version = Version(withTeam);
        var planned = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition", new { targetState = "planned", reason = (string?)null, version }));
        version = Version(planned);
        var started = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition", new { targetState = "in_progress", reason = (string?)null, version }));
        var itemId = started.GetProperty("checklistItems")[0].GetProperty("id").GetGuid();

        var draft = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/items/{itemId}/responses",
            new { verdict = (string?)null, comment = "wip", isDraft = true, version = await VersionAsync(admin, auditId) }));
        var responseId = draft.GetProperty("id").GetGuid();

        // Bytes that are not a real PDF, declared as application/pdf → magic-byte check fails → 422.
        using var form = new MultipartFormDataContent();
        var bad = new ByteArrayContent(Encoding.ASCII.GetBytes("this is not a pdf"));
        bad.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(bad, "file", "fake.pdf");
        var resp = await admin.PostAsync($"/api/v1/audits/{auditId}/responses/{responseId}/evidence", form);
        Assert.Equal(HttpStatusCode.UnprocessableContent, resp.StatusCode);
    }
}
