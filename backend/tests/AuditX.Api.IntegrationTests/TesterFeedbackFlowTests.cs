using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AuditX.Api.IntegrationTests;

/// <summary>
/// Rules raised by user-acceptance testing: segregation of duties on plan approval, no roles or ownership for
/// deactivated accounts, input validation on organisation identity / support engineers / user names, reference-data
/// sort positions and label history, org-unit types with an auditable deactivate/reactivate, and named response sets.
/// </summary>
public sealed class TesterFeedbackFlowTests(ApiFactory factory) : IClassFixture<ApiFactory>
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
        Assert.True(response.IsSuccessStatusCode, $"{(int)response.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.GetProperty("data").Clone();
    }

    private static string Version(JsonElement node) => node.GetProperty("version").GetString()!;

    private static async Task<Dictionary<string, Guid>> UsersByEmailAsync(HttpClient admin)
        => (await DataAsync(await admin.GetAsync("/api/v1/users?limit=200"))).GetProperty("items").EnumerateArray()
            .ToDictionary(u => u.GetProperty("email").GetString()!, u => u.GetProperty("id").GetGuid());

    private static async Task<Guid> CreateUserAsync(HttpClient admin, string first = "Test", string last = "Person")
    {
        var created = await DataAsync(await admin.PostAsJsonAsync("/api/v1/users", new
        {
            email = $"uat.{Guid.NewGuid():N}@example.com",
            firstName = first,
            lastName = last,
            externalId = (string?)null,
            roleNames = (string[]?)null,
        }));
        return created.GetProperty("id").GetGuid();
    }

    // ---- Segregation of duties ------------------------------------------------------------------------------------------

    [Fact]
    public async Task Whoever_submits_a_plan_cannot_also_record_its_decision()
    {
        var admin = await LoginAsync("admin");
        var entity = await DataAsync(await admin.PostAsJsonAsync("/api/v1/audit-universe/entities", new { name = $"SoD {Guid.NewGuid():N}", entityType = "process" }));
        var year = 3000 + Random.Shared.Next(0, 5000);
        var plan = await DataAsync(await admin.PostAsJsonAsync("/api/v1/annual-plans", new
        {
            periodLabel = $"FY{year}",
            periodStart = $"{year}-01-01",
            periodEnd = $"{year}-12-31",
        }));
        var planId = plan.GetProperty("id").GetGuid();
        (await admin.PostAsJsonAsync($"/api/v1/annual-plans/{planId}/items", new
        {
            entityIds = new[] { entity.GetProperty("id").GetGuid() },
            auditType = "process_review",
            plannedStartDate = $"{year}-03-01",
            plannedEndDate = $"{year}-03-31",
            estimatedEffortDays = 5,
        })).EnsureSuccessStatusCode();
        await DataAsync(await admin.PostAsync($"/api/v1/annual-plans/{planId}/submit", null));

        var selfApproval = await admin.PostAsJsonAsync($"/api/v1/annual-plans/{planId}/decision",
            new { decision = "Approved", detail = (string?)null, comments = Array.Empty<string>() });

        Assert.Equal(HttpStatusCode.Forbidden, selfApproval.StatusCode);
        Assert.Contains("someone else", await selfApproval.Content.ReadAsStringAsync());
    }

    // ---- Deactivated accounts -------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_deactivated_account_can_be_given_no_role_and_own_no_entity()
    {
        var admin = await LoginAsync("admin");
        var userId = await CreateUserAsync(admin);
        (await admin.PatchAsJsonAsync($"/api/v1/users/{userId}", new { status = "deactivated" })).EnsureSuccessStatusCode();

        var roles = (await DataAsync(await admin.GetAsync("/api/v1/roles"))).EnumerateArray()
            .ToDictionary(r => r.GetProperty("name").GetString()!, r => r.GetProperty("id").GetGuid());
        var grant = await admin.PostAsJsonAsync($"/api/v1/users/{userId}/roles", new { roleId = roles["Auditor"], scopeValue = (string?)null });
        Assert.Equal(HttpStatusCode.Conflict, grant.StatusCode);

        // Not offered as an owner…
        var assignable = await DataAsync(await admin.GetAsync("/api/v1/users/assignable"));
        Assert.DoesNotContain(assignable.EnumerateArray(), u => u.GetProperty("id").GetGuid() == userId);

        // …and refused by the server when sent anyway.
        var owned = await admin.PostAsJsonAsync("/api/v1/audit-universe/entities",
            new { name = $"Owned {Guid.NewGuid():N}", entityType = "process", ownerUserId = userId });
        Assert.Equal(HttpStatusCode.Conflict, owned.StatusCode);
    }

    [Fact]
    public async Task An_administrator_cannot_deactivate_their_own_account_singly_or_in_bulk()
    {
        var admin = await LoginAsync("admin");
        var adminId = (await UsersByEmailAsync(admin))["admin@auditx.local"];
        var other = await CreateUserAsync(admin);

        var single = await admin.PatchAsJsonAsync($"/api/v1/users/{adminId}", new { status = "deactivated" });
        Assert.Equal(HttpStatusCode.Conflict, single.StatusCode);

        // Bulk is all-or-nothing: the other account is left alone too.
        var bulk = await DataAsync(await admin.PostAsJsonAsync("/api/v1/admin/users/bulk-deactivate", new { userIds = new[] { adminId, other } }));
        Assert.Equal(0, bulk.GetProperty("successCount").GetInt32());
        Assert.Contains(bulk.GetProperty("errors").EnumerateArray(), e => e.GetProperty("identifier").GetString() == adminId.ToString());

        // Still signed in.
        (await admin.GetAsync("/api/v1/users/me")).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Eligible_owners_come_with_the_roles_they_hold()
    {
        var admin = await LoginAsync("admin");
        var users = await UsersByEmailAsync(admin);

        var assignable = (await DataAsync(await admin.GetAsync("/api/v1/users/assignable"))).EnumerateArray().ToArray();
        var adminRow = assignable.Single(u => u.GetProperty("id").GetGuid() == users["admin@auditx.local"]);

        Assert.True(adminRow.GetProperty("hasRole").GetBoolean());
        Assert.NotEmpty(adminRow.GetProperty("roles").EnumerateArray());
    }

    // ---- Input validation -----------------------------------------------------------------------------------------------

    [Fact]
    public async Task The_organisation_display_name_must_be_a_real_name()
    {
        var admin = await LoginAsync("admin");
        var current = await DataAsync(await admin.GetAsync("/api/v1/admin/institution-settings"));

        var response = await admin.PatchAsJsonAsync("/api/v1/admin/institution-settings", new
        {
            institutionDisplayName = "####",
            timezone = current.GetProperty("timezone").GetString(),
            localeDefault = current.GetProperty("localeDefault").GetString(),
            adProvisioningFilterOuDn = (string?)null,
            adProvisioningFilterGroupSid = (string?)null,
            allowOverlappingPlanPeriods = current.GetProperty("allowOverlappingPlanPeriods").GetBoolean(),
            allowAuditLaunchBeforeApproval = current.GetProperty("allowAuditLaunchBeforeApproval").GetBoolean(),
            primaryColor = current.GetProperty("primaryColor").GetString(),
            accentColor = current.GetProperty("accentColor").GetString(),
            logoDataUri = (string?)null,
            iconDataUri = (string?)null,
            showOverview = current.GetProperty("showOverview").GetBoolean(),
            showWalkthrough = current.GetProperty("showWalkthrough").GetBoolean(),
            autoStartWalkthrough = current.GetProperty("autoStartWalkthrough").GetBoolean(),
            idleTimeoutMinutes = current.GetProperty("idleTimeoutMinutes").GetInt32(),
            idleWarningSeconds = current.GetProperty("idleWarningSeconds").GetInt32(),
            requireIndependenceDeclaration = current.GetProperty("requireIndependenceDeclaration").GetBoolean(),
        });

        Assert.Equal(HttpStatusCode.UnprocessableContent, response.StatusCode);
    }

    [Fact]
    public async Task Support_engineers_are_identified_by_email_address()
    {
        var admin = await LoginAsync("admin");

        var response = await admin.PostAsJsonAsync("/api/v1/admin/support-channel/enable",
            new { engineerIdentifiers = new[] { "not-an-email" }, durationMinutes = 60 });

        Assert.Equal(HttpStatusCode.UnprocessableContent, response.StatusCode);
    }

    [Fact]
    public async Task A_user_needs_a_first_and_last_name()
    {
        var admin = await LoginAsync("admin");

        var response = await admin.PostAsJsonAsync("/api/v1/users", new
        {
            email = $"noname.{Guid.NewGuid():N}@example.com",
            firstName = " ",
            lastName = "",
            externalId = (string?)null,
            roleNames = (string[]?)null,
        });

        Assert.Equal(HttpStatusCode.UnprocessableContent, response.StatusCode);
    }

    // ---- Reference data -------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Sort_positions_are_unique_and_a_rename_keeps_the_earlier_label_on_record()
    {
        var admin = await LoginAsync("admin");
        const string category = "root_cause_category";
        var existing = (await DataAsync(await admin.GetAsync($"/api/v1/reference-data/{category}?includeInactive=true"))).EnumerateArray().ToArray();
        var freeSort = existing.Length == 0 ? 1 : existing.Max(i => i.GetProperty("sortOrder").GetInt32()) + 1;
        var code = $"uat_{Guid.NewGuid():N}"[..20];

        var created = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/reference-data/{category}",
            new { code, label = "Process gap", description = (string?)null, sortOrder = freeSort }));
        var id = created.GetProperty("id").GetGuid();

        // A second item at the same position is refused.
        var clash = await admin.PostAsJsonAsync($"/api/v1/reference-data/{category}",
            new { code = code + "b", label = "Another", description = (string?)null, sortOrder = freeSort });
        Assert.Equal(HttpStatusCode.Conflict, clash.StatusCode);

        // A rename is allowed, and the label it had before stays on record.
        var renamed = await DataAsync(await admin.PatchAsJsonAsync($"/api/v1/reference-data/{category}/{id}",
            new { label = "Process design gap", description = (string?)null, sortOrder = freeSort }));
        Assert.Equal("Process design gap", renamed.GetProperty("label").GetString());
        var history = renamed.GetProperty("labelHistory").EnumerateArray().Select(v => v.GetProperty("label").GetString()).ToArray();
        Assert.Equal(["Process gap", "Process design gap"], history);
    }

    // ---- Org units ------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task An_org_unit_has_a_type_and_its_deactivation_and_reactivation_are_both_in_the_trail()
    {
        var admin = await LoginAsync("admin");
        var unit = await DataAsync(await admin.PostAsJsonAsync("/api/v1/org-units",
            new { name = $"Unit {Guid.NewGuid():N}"[..20], code = $"U{Random.Shared.Next(100000, 999999)}", parentOrgUnitId = (Guid?)null, unitType = "business_unit" }));
        var id = unit.GetProperty("id").GetGuid();
        Assert.Equal("business_unit", unit.GetProperty("unitType").GetString());

        var unknownType = await admin.PatchAsJsonAsync($"/api/v1/org-units/{id}/type", new { unitType = "galaxy" });
        Assert.Equal(HttpStatusCode.UnprocessableContent, unknownType.StatusCode);

        var retyped = await DataAsync(await admin.PatchAsJsonAsync($"/api/v1/org-units/{id}/type", new { unitType = "location" }));
        Assert.Equal("location", retyped.GetProperty("unitType").GetString());

        await DataAsync(await admin.PostAsync($"/api/v1/org-units/{id}/archive", null));
        await DataAsync(await admin.PostAsync($"/api/v1/org-units/{id}/restore", null));

        var trail = await admin.GetStringAsync($"/api/v1/audit-trail/object/org_unit/{id}");
        Assert.Contains("org_unit_archived", trail);
        Assert.Contains("org_unit_restored", trail);
    }

    // ---- Named response sets --------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_named_response_set_is_offered_on_the_items_that_choose_it_and_once_retired_cannot_be_newly_chosen()
    {
        var admin = await LoginAsync("admin");
        var users = await UsersByEmailAsync(admin);
        const string options = """[{"code":"eff","label":"Effective","order":0,"score":100,"isDeficiency":false,"isNotApplicable":false,"requiresComment":false},{"code":"part","label":"Partially effective","order":1,"score":50,"isDeficiency":true,"isNotApplicable":false,"requiresComment":true},{"code":"ineff","label":"Ineffective","order":2,"score":0,"isDeficiency":true,"isNotApplicable":false,"requiresComment":true}]""";

        var name = $"Control effectiveness {Guid.NewGuid():N}"[..40];
        var set = await DataAsync(await admin.PostAsJsonAsync("/api/v1/response-option-sets/named",
            new { name, responseType = "pass_fail_na", optionsJson = options }));
        var setId = set.GetProperty("id").GetGuid();

        // Names are unique.
        var duplicate = await admin.PostAsJsonAsync("/api/v1/response-option-sets/named",
            new { name, responseType = "pass_fail_na", optionsJson = options });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        // An audit checklist item chooses the set; its answer form offers that set's options.
        var audit = await DataAsync(await admin.PostAsJsonAsync("/api/v1/audits", new
        {
            name = $"Response set {Guid.NewGuid():N}",
            auditType = "branch",
            startDate = "2027-01-10",
            targetEndDate = "2027-02-10",
            leadUserId = users["admin@auditx.local"],
            auditeeUserId = users["auditee@auditx.local"],
        }));
        var auditId = audit.GetProperty("id").GetGuid();
        var config = $$"""{"optionSetId":"{{setId}}"}""";
        var withItem = await DataAsync(await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/checklist/items",
            new { prompt = "Is the control effective?", responseType = "pass_fail_na", responseConfigJson = config, isRequired = true, version = Version(audit) }));
        var itemId = withItem.GetProperty("checklistItems").EnumerateArray().Single().GetProperty("id").GetGuid();

        var input = await DataAsync(await admin.GetAsync($"/api/v1/audits/{auditId}/items/{itemId}/response-input"));
        Assert.Equal(["Effective", "Partially effective", "Ineffective"],
            input.GetProperty("options").EnumerateArray().Select(o => o.GetProperty("label").GetString()));

        // Retired: no new item may choose it…
        await DataAsync(await admin.PostAsync($"/api/v1/response-option-sets/named/{setId}/retire", null));
        var newItem = await admin.PostAsJsonAsync($"/api/v1/audits/{auditId}/checklist/items",
            new { prompt = "Another", responseType = "pass_fail_na", responseConfigJson = config, isRequired = true, version = Version(withItem) });
        Assert.Equal(HttpStatusCode.Conflict, newItem.StatusCode);

        // …but the item already using it keeps its options.
        var still = await DataAsync(await admin.GetAsync($"/api/v1/audits/{auditId}/items/{itemId}/response-input"));
        Assert.Equal(3, still.GetProperty("options").GetArrayLength());
    }
}
