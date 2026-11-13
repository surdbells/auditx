using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace AuditX.Api.IntegrationTests;

/// <summary>
/// P2-D expected/requested evidence: requesting, receiving and waiving evidence against an audit, listing, the
/// received/outstanding lifecycle, permission gating, and the requested-vs-received analytics.
/// </summary>
public sealed class EvidenceRequestFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
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

    private async Task<HttpClient> AnalystAsync(HttpClient admin)
    {
        var users = await UsersByEmailAsync(admin);
        var roles = await DataAsync(await admin.GetAsync("/api/v1/roles"));
        var roleId = roles.EnumerateArray().First(r => r.GetProperty("name").GetString() == "Audit Manager").GetProperty("id").GetGuid();
        await admin.PostAsJsonAsync($"/api/v1/users/{users["manager@auditx.local"]}/roles", new { roleId, scopeValue = (string?)null });
        return await LoginAsync("manager");
    }

    private async Task<Guid> SeedInProgressAuditAsync(HttpClient admin)
    {
        var users = await UsersByEmailAsync(admin);
        var created = await DataAsync(await admin.PostAsJsonAsync("/api/v1/audits", new
        {
            name = $"Evi {Guid.NewGuid():N}",
            auditType = "branch",
            startDate = "2027-01-10",
            targetEndDate = "2027-02-10",
            leadUserId = users["manager@auditx.local"],
            auditeeUserId = users["auditee@auditx.local"],
        }));
        var auditId = created.GetProperty("id").GetGuid();
        var version = Version(created);

        var withItem = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/checklist/items",
            new { prompt = "Q0", responseType = "pass_fail_na", isRequired = true, version }));
        version = Version(withItem);
        var withTeam = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/team",
            new { userId = users["auditor@auditx.local"], teamRole = "auditor", version }));
        version = Version(withTeam);
        var planned = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition", new { targetState = "planned", reason = (string?)null, version }));
        version = Version(planned);
        await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/transition", new { targetState = "in_progress", reason = (string?)null, version }));
        return auditId;
    }

    [Fact]
    public async Task Request_receive_waive_and_evidence_analytics()
    {
        var admin = await LoginAsync("admin"); // RespondItem + ManageAudit
        var users = await UsersByEmailAsync(admin);
        var auditee = users["auditee@auditx.local"];
        var auditId = await SeedInProgressAuditAsync(admin);

        var req1 = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/evidence-requests", new
        {
            requestedFromUserId = auditee, title = "Signed dual-authorisation matrix", documentType = "approval_signoff", dueDate = "2027-01-25",
        }));
        Assert.Equal("requested", req1.GetProperty("status").GetString());
        Assert.False(req1.GetProperty("isOverdue").GetBoolean());

        var req2 = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/evidence-requests", new
        {
            requestedFromUserId = auditee, title = "Reconciliation for December", documentType = "reconciliation",
        }));

        // Receive the first, waive the second.
        var received = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/evidence-requests/{req1.GetProperty("id").GetGuid()}/received",
            new { version = Version(req1) }));
        Assert.Equal("received", received.GetProperty("status").GetString());

        var waived = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/evidence-requests/{req2.GetProperty("id").GetGuid()}/waive",
            new { reason = "Superseded by an automated control; no longer applicable.", version = Version(req2) }));
        Assert.Equal("waived", waived.GetProperty("status").GetString());
        // A waived request was never received — its received fields stay null.
        Assert.Equal(JsonValueKind.Null, waived.GetProperty("receivedByUserId").ValueKind);
        Assert.Equal(JsonValueKind.Null, waived.GetProperty("receivedAt").ValueKind);

        // A received request cannot be waived again.
        var reWaive = await admin.PostAsJsonAsync($"/api/v1/evidence-requests/{req1.GetProperty("id").GetGuid()}/waive",
            new { reason = "Trying to waive an already-received item.", version = Version(received) });
        Assert.Equal(HttpStatusCode.Conflict, reWaive.StatusCode);

        // Both appear in the per-audit list.
        var list = await DataAsync(await admin.GetAsync($"/api/v1/audits/{auditId}/evidence-requests"));
        Assert.Equal(2, list.GetArrayLength());

        // Evidence analytics reflect the received + waived.
        var analyst = await AnalystAsync(admin);
        var summary = await DataAsync(await analyst.GetAsync("/api/v1/analytics/evidence"));
        Assert.True(summary.GetProperty("received").GetInt32() >= 1);
        Assert.True(summary.GetProperty("waived").GetInt32() >= 1);
    }

    [Fact]
    public async Task Auditee_sees_the_request_uploads_a_document_and_it_shows_to_the_auditor()
    {
        var admin = await LoginAsync("admin");
        var users = await UsersByEmailAsync(admin);
        var auditeeId = users["auditee@auditx.local"];
        var auditId = await SeedInProgressAuditAsync(admin);

        // The auditor requests a document from the auditee.
        var req = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/evidence-requests", new
        {
            requestedFromUserId = auditeeId, purpose = "review_document", title = "Board-approved SoD policy", documentType = "policy_procedure",
        }));
        var requestId = req.GetProperty("id").GetGuid();
        Assert.Equal(auditeeId, req.GetProperty("requestedFromUserId").GetGuid());
        Assert.Empty(req.GetProperty("files").EnumerateArray());

        // The auditee sees it on their own worklist.
        var auditee = await LoginAsync("auditee");
        var mine = await DataAsync(await auditee.GetAsync("/api/v1/my/evidence-requests?outstandingOnly=true"));
        Assert.Contains(mine.EnumerateArray(), r => r.GetProperty("id").GetGuid() == requestId);

        // The auditee uploads the document (valid %PDF).
        var pdf = Encoding.ASCII.GetBytes("%PDF-1.4\n%AuditX requested document\n");
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(pdf);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(fileContent, "file", "sod-policy.pdf");
        var uploaded = await DataAsync(await auditee.PostAsync($"/api/v1/evidence-requests/{requestId}/files", form));

        // Upload marks the request received and attaches the file.
        Assert.Equal("received", uploaded.GetProperty("status").GetString());
        Assert.Single(uploaded.GetProperty("files").EnumerateArray());

        // It now shows on the auditor's screen (the per-audit list) with the uploaded document + download works.
        var list = await DataAsync(await admin.GetAsync($"/api/v1/audits/{auditId}/evidence-requests"));
        var row = list.EnumerateArray().First(r => r.GetProperty("id").GetGuid() == requestId);
        Assert.Equal("received", row.GetProperty("status").GetString());
        var evidenceId = row.GetProperty("files")[0].GetProperty("id").GetGuid();
        var download = await admin.GetAsync($"/api/v1/audits/{auditId}/evidence/{evidenceId}");
        download.EnsureSuccessStatusCode();
        Assert.Equal(pdf.Length, (await download.Content.ReadAsByteArrayAsync()).Length);
    }

    [Fact]
    public async Task Only_the_requested_auditee_may_upload_against_a_request()
    {
        var admin = await LoginAsync("admin");
        var users = await UsersByEmailAsync(admin);
        var auditId = await SeedInProgressAuditAsync(admin);

        // Request from the auditee.
        var req = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/evidence-requests", new
        {
            requestedFromUserId = users["auditee@auditx.local"], title = "Requested from the auditee",
        }));
        var requestId = req.GetProperty("id").GetGuid();

        // A different user (the auditor, not the recipient, no ManageAudit) cannot upload → 403.
        var auditor = await LoginAsync("auditor");
        var pdf = Encoding.ASCII.GetBytes("%PDF-1.4\n");
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(pdf);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(fileContent, "file", "x.pdf");
        var resp = await auditor.PostAsync($"/api/v1/evidence-requests/{requestId}/files", form);
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    [Fact]
    public async Task Requesting_evidence_requires_the_respond_permission()
    {
        var admin = await LoginAsync("admin");
        var auditId = await SeedInProgressAuditAsync(admin);

        var auditee = await LoginAsync("auditee"); // no RespondItem
        var resp = await auditee.PostAsJsonAsync($"/api/v1/audits/{auditId}/evidence-requests", new
        {
            title = "Auditee should not be able to request evidence.",
        });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    private static MultipartFormDataContent Pdf(string filename, string marker)
    {
        var form = new MultipartFormDataContent();
        var content = new ByteArrayContent(Encoding.ASCII.GetBytes($"%PDF-1.4\n%{marker}\n"));
        content.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        form.Add(content, "file", filename);
        return form;
    }

    /// <summary>
    /// An auditee can RECALL a document they uploaded (e.g. the wrong file) — but nothing is erased: the document
    /// stays on the record marked recalled with who / when / why, the recall is in the audit trail, and the audit
    /// team can no longer open it. With no document left, the request is outstanding again and a replacement can
    /// be uploaded alongside the recalled one.
    /// </summary>
    [Fact]
    public async Task An_auditee_can_recall_an_uploaded_document_and_the_history_remains()
    {
        var admin = await LoginAsync("admin"); // requests the evidence; holds ManageEvidence (evidence custodian)
        var users = await UsersByEmailAsync(admin);
        var auditeeId = users["auditee@auditx.local"];
        var auditId = await SeedInProgressAuditAsync(admin);
        var analyst = await AnalystAsync(admin); // the audit's lead (manager) — on the team

        var req = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/evidence-requests", new
        {
            requestedFromUserId = auditeeId, title = "December reconciliation", documentType = "reconciliation",
        }));
        var requestId = req.GetProperty("id").GetGuid();

        var auditee = await LoginAsync("auditee");
        JsonElement uploaded;
        using (var form = Pdf("wrong-month.pdf", "November, not December"))
        {
            uploaded = await DataAsync(await auditee.PostAsync($"/api/v1/evidence-requests/{requestId}/files", form));
        }

        Assert.Equal("received", uploaded.GetProperty("status").GetString());
        var file = uploaded.GetProperty("files")[0];
        var fileId = file.GetProperty("id").GetGuid();
        var sha256 = file.GetProperty("sha256Hash").GetString();
        Assert.False(file.GetProperty("isRecalled").GetBoolean());

        // ---- A reason is mandatory, and only the uploader can recall.
        var noReason = await auditee.PostAsJsonAsync($"/api/v1/evidence-requests/{requestId}/files/{fileId}/recall", new { reason = "  " });
        Assert.Equal(HttpStatusCode.UnprocessableContent, noReason.StatusCode);

        var notUploader = await admin.PostAsJsonAsync($"/api/v1/evidence-requests/{requestId}/files/{fileId}/recall", new { reason = "Not mine to recall" });
        Assert.Equal(HttpStatusCode.Forbidden, notUploader.StatusCode);

        // ---- The auditee recalls it.
        var recalled = await DataAsync(await auditee.PostAsJsonAsync($"/api/v1/evidence-requests/{requestId}/files/{fileId}/recall",
            new { reason = "Uploaded the November reconciliation by mistake." }));

        // Nothing is on file any more, so the request is outstanding again…
        Assert.Equal("requested", recalled.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, recalled.GetProperty("receivedAt").ValueKind);

        // …but the document is STILL listed — marked recalled, with who / when / why, and its hash intact.
        var kept = Assert.Single(recalled.GetProperty("files").EnumerateArray());
        Assert.Equal(fileId, kept.GetProperty("id").GetGuid());
        Assert.True(kept.GetProperty("isRecalled").GetBoolean());
        Assert.Equal(auditeeId, kept.GetProperty("recalledBy").GetGuid());
        Assert.NotEqual(JsonValueKind.Null, kept.GetProperty("recalledAt").ValueKind);
        Assert.Equal("Uploaded the November reconciliation by mistake.", kept.GetProperty("recallReason").GetString());
        Assert.Equal("wrong-month.pdf", kept.GetProperty("originalFilename").GetString());
        Assert.Equal(sha256, kept.GetProperty("sha256Hash").GetString());

        // The auditor's list shows the same history.
        var auditorRow = (await DataAsync(await analyst.GetAsync($"/api/v1/audits/{auditId}/evidence-requests")))
            .EnumerateArray().First(r => r.GetProperty("id").GetGuid() == requestId);
        Assert.True(auditorRow.GetProperty("files")[0].GetProperty("isRecalled").GetBoolean());

        // It cannot be recalled twice.
        var again = await auditee.PostAsJsonAsync($"/api/v1/evidence-requests/{requestId}/files/{fileId}/recall", new { reason = "Again" });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        // ---- An auditor on the team (can view evidence, is not an evidence custodian) can no longer open it…
        var roles = await DataAsync(await admin.GetAsync("/api/v1/roles"));
        var auditorRole = roles.EnumerateArray().First(r => r.GetProperty("name").GetString() == "Auditor").GetProperty("id").GetGuid();
        await admin.PostAsJsonAsync($"/api/v1/users/{users["auditor@auditx.local"]}/roles", new { roleId = auditorRole, scopeValue = (string?)null });
        var auditor = await LoginAsync("auditor");
        var teamDownload = await auditor.GetAsync($"/api/v1/audits/{auditId}/evidence/{fileId}");
        Assert.Equal(HttpStatusCode.Conflict, teamDownload.StatusCode);

        // …but its content was retained (recall can never destroy evidence): an evidence custodian (ManageEvidence —
        // audit managers and administrators) can still reach it, and that access is logged.
        var custodianDownload = await admin.GetAsync($"/api/v1/audits/{auditId}/evidence/{fileId}");
        custodianDownload.EnsureSuccessStatusCode();
        Assert.Contains("November, not December", Encoding.ASCII.GetString(await custodianDownload.Content.ReadAsByteArrayAsync()));

        // ---- The recall is on the audit trail — against the document and against the request.
        var fileTrail = await DataAsync(await admin.GetAsync($"/api/v1/audit-trail/object/evidence_file/{fileId}"));
        var fileEvents = fileTrail.GetProperty("items").EnumerateArray().Select(e => e.GetProperty("eventType").GetString()).ToArray();
        Assert.Contains("evidence_uploaded", fileEvents);
        Assert.Contains("evidence_recalled", fileEvents);
        Assert.Contains("evidence_recalled_accessed", fileEvents); // the custodian's access is itself logged
        var requestTrail = await DataAsync(await admin.GetAsync($"/api/v1/audit-trail/object/evidence_request/{requestId}"));
        Assert.Contains(requestTrail.GetProperty("items").EnumerateArray(), e => e.GetProperty("eventType").GetString() == "evidence_recalled");

        // ---- The auditee provides the right document: received again, and BOTH documents are on the record.
        JsonElement replaced;
        using (var form = Pdf("december.pdf", "December"))
        {
            replaced = await DataAsync(await auditee.PostAsync($"/api/v1/evidence-requests/{requestId}/files", form));
        }

        Assert.Equal("received", replaced.GetProperty("status").GetString());
        var files = replaced.GetProperty("files").EnumerateArray().ToArray();
        Assert.Equal(2, files.Length);
        Assert.Single(files, f => f.GetProperty("isRecalled").GetBoolean());
        Assert.Single(files, f => !f.GetProperty("isRecalled").GetBoolean());
    }

    /// <summary>Recalling one of several documents leaves the request received — evidence is still on file.</summary>
    [Fact]
    public async Task Recalling_one_of_several_documents_keeps_the_request_received()
    {
        var admin = await LoginAsync("admin");
        var users = await UsersByEmailAsync(admin);
        var auditId = await SeedInProgressAuditAsync(admin);
        var req = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/evidence-requests", new
        {
            requestedFromUserId = users["auditee@auditx.local"], title = "Signed approvals",
        }));
        var requestId = req.GetProperty("id").GetGuid();

        var auditee = await LoginAsync("auditee");
        JsonElement after;
        using (var form = Pdf("approval-1.pdf", "one"))
        {
            await DataAsync(await auditee.PostAsync($"/api/v1/evidence-requests/{requestId}/files", form));
        }

        using (var form = Pdf("approval-2.pdf", "two"))
        {
            after = await DataAsync(await auditee.PostAsync($"/api/v1/evidence-requests/{requestId}/files", form));
        }

        var first = after.GetProperty("files")[0].GetProperty("id").GetGuid();
        var recalled = await DataAsync(await auditee.PostAsJsonAsync($"/api/v1/evidence-requests/{requestId}/files/{first}/recall",
            new { reason = "Superseded by the second scan." }));

        Assert.Equal("received", recalled.GetProperty("status").GetString());
        Assert.Equal(2, recalled.GetProperty("files").GetArrayLength());
    }

    /// <summary>
    /// Independence: the person a document is requested FROM cannot decide that request's fate — even when they are
    /// an auditor on the audit's own team with the permission to manage requests.
    /// </summary>
    [Fact]
    public async Task The_recipient_of_a_request_cannot_mark_it_received_waive_or_delete_it()
    {
        var admin = await LoginAsync("admin");
        var users = await UsersByEmailAsync(admin);
        var auditId = await SeedInProgressAuditAsync(admin); // 'auditor' is on this audit's team

        // Give the auditor their role (RespondItem → may manage evidence requests on audits they are on).
        var roles = await DataAsync(await admin.GetAsync("/api/v1/roles"));
        var auditorRole = roles.EnumerateArray().First(r => r.GetProperty("name").GetString() == "Auditor").GetProperty("id").GetGuid();
        await admin.PostAsJsonAsync($"/api/v1/users/{users["auditor@auditx.local"]}/roles", new { roleId = auditorRole, scopeValue = (string?)null });
        var auditor = await LoginAsync("auditor");

        // A document is requested FROM that auditor.
        var req = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/evidence-requests", new
        {
            requestedFromUserId = users["auditor@auditx.local"], title = "Requested from someone on the team",
        }));
        var requestId = req.GetProperty("id").GetGuid();
        var version = Version(req);

        Assert.Equal(HttpStatusCode.Forbidden,
            (await auditor.PostAsJsonAsync($"/api/v1/evidence-requests/{requestId}/received", new { version })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await auditor.PostAsJsonAsync($"/api/v1/evidence-requests/{requestId}/waive", new { reason = "I would rather not provide this.", version })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await auditor.DeleteAsync($"/api/v1/evidence-requests/{requestId}?version={Uri.EscapeDataString(version)}")).StatusCode);

        // They can still do the one thing asked of them — provide the document.
        using var form = Pdf("requested.pdf", "provided");
        var uploaded = await DataAsync(await auditor.PostAsync($"/api/v1/evidence-requests/{requestId}/files", form));
        Assert.Equal("received", uploaded.GetProperty("status").GetString());
    }
}
