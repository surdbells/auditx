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
