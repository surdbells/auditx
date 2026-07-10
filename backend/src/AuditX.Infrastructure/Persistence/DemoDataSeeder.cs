using System.Security.Cryptography;
using System.Text;
using AuditX.Application.Ac;
using AuditX.Application.Ac.Generation;
using AuditX.Application.Abstractions;
using AuditX.Application.Abstractions.Integrations;
using AuditX.Application.Abstractions.Storage;
using AuditX.Application.Analytics.Services;
using AuditX.Application.Audits.Services;
using AuditX.Application.Configuration;
using AuditX.Application.Reports.Generation;
using AuditX.Application.Sanctions;
using AuditX.Domain.Ac;
using AuditX.Domain.Authorization;
using AuditX.Domain.Configuration;
using AuditX.Domain.Enums;
using AuditX.Domain.Evidence;
using AuditX.Domain.Exceptions;
using AuditX.Domain.Identity;
using AuditX.Domain.Integrations;
using AuditX.Domain.Planning;
using AuditX.Domain.Reports;
using AuditX.Domain.Sanctions;
using AuditX.Domain.Templates;
using AuditX.Domain.Universe;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AuditX.Infrastructure.Persistence;

/// <summary>
/// Rich, interconnected DEMO dataset spanning every module (M1–M15), built through the real domain aggregates and
/// committed via <see cref="AppDbContext.SaveChangesAsync(CancellationToken)"/> so invariants hold and the audit
/// trail / domain events fire authentically. Runs ONLY when <c>Database:SeedDemoData</c> is true (never in
/// production by default) and is idempotent — an existing audit means the demo data is already present.
///
/// <para>Ordering matters: users → templates → universe + plan → audits → responses + evidence → exceptions + MAP →
/// sanctions → reports → analytics scan → config draft → AC pack → integrations → admin. The report/AC-pack
/// generation and the recurrence scan are driven through the same Application services the API uses, so the demo
/// exercises the real generation/analytics paths.</para>
/// </summary>
public sealed class DemoDataSeeder(
    AppDbContext db,
    IClock clock,
    IFileStorage storage,
    ICredentialProtector credentialProtector,
    AuditCreationService auditCreationService,
    ReportGenerationService reportGeneration,
    AcPackGenerationService acPackGeneration,
    RecurrenceClusterService recurrenceScan,
    ILogger<DemoDataSeeder> logger)
{
    // A fixed reference "now" is derived from the injected clock at run start so every relative date is coherent.
    private DateTimeOffset _now;
    private DateOnly Today => DateOnly.FromDateTime(_now.UtcDateTime);

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        // Idempotency: an existing audit means the demo dataset has already been seeded. Bail cheaply.
        if (await db.Audits.IgnoreQueryFilters().AnyAsync(cancellationToken))
        {
            logger.LogInformation("Demo data already present (an audit exists); skipping DemoDataSeeder.");
            return;
        }

        _now = clock.UtcNow;

        var users = await SeedUsersAsync(cancellationToken);
        var templates = await SeedTemplatesAsync(cancellationToken);
        var (entities, plan) = await SeedUniverseAndPlanAsync(users, cancellationToken);
        await SeedOrgUnitsAsync(cancellationToken);
        var audits = await SeedAuditsAsync(users, templates, plan, entities, cancellationToken);
        await SeedResponsesAndEvidenceAsync(audits, users, cancellationToken);
        var exceptions = await SeedExceptionsAndMapsAsync(audits, entities, users, cancellationToken);
        await SeedSanctionsAsync(exceptions, users, cancellationToken);
        await SeedReportsAsync(audits, cancellationToken);
        await SeedAnalyticsAsync(cancellationToken);
        await SeedConfigurationDraftAsync(cancellationToken);
        await SeedAuditCommitteeAsync(users, cancellationToken);
        await SeedIntegrationsAsync(cancellationToken);
        await SeedAdministrationAsync(cancellationToken);

        logger.LogInformation("DemoDataSeeder completed: rich demo dataset seeded across all modules.");
    }

    // ---------------------------------------------------------------------------------------------------------
    // M1 — Users + roles. The four dev users can log in (via the Development identity provider); this seeder
    // (a) assigns rich business roles to the loginable dev users so every persona's SCREENS are reachable, and
    // (b) provisions additional named directory users purely as DATA ACTORS (team members, owners, AC members)
    // so every list/detail screen shows realistic, populated data. The 12 personas below cover every role.
    // ---------------------------------------------------------------------------------------------------------
    private sealed record DemoUsers(
        Guid AdminId,
        Guid Manager1Id,
        Guid Manager2Id,
        Guid Auditor1Id,
        Guid Auditor2Id,
        Guid Auditor3Id,
        Guid Auditee1Id,
        Guid Auditee2Id,
        Guid Auditee3Id,
        Guid HrRepId,
        Guid DcMember1Id,
        Guid DcMember2Id,
        Guid AppealsId,
        Guid AcMember1Id,
        Guid AcMember2Id,
        Guid AcMember3Id,
        Guid AcChairId,
        Guid CiaId);

    private async Task<DemoUsers> SeedUsersAsync(CancellationToken ct)
    {
        var rolesByName = await db.Roles.ToDictionaryAsync(r => r.Name, StringComparer.Ordinal, ct);

        // The four seeded dev users already exist (DbSeeder provisioned them). Resolve them and grant business roles
        // so the demonstrator can log in as admin/manager/auditor/auditee and land on populated persona screens.
        var admin = await db.Users.IgnoreQueryFilters().FirstAsync(u => u.AdSamAccountName == "admin", ct);
        var manager = await db.Users.IgnoreQueryFilters().FirstAsync(u => u.AdSamAccountName == "manager", ct);
        var auditor = await db.Users.IgnoreQueryFilters().FirstAsync(u => u.AdSamAccountName == "auditor", ct);
        var auditee = await db.Users.IgnoreQueryFilters().FirstAsync(u => u.AdSamAccountName == "auditee", ct);

        // Grant the loginable dev users a spread of roles so their sessions can reach the whole platform.
        GrantRole(manager, rolesByName, BuiltInRoles.AuditManagerName);
        GrantRole(manager, rolesByName, AcRoles.ChiefInternalAuditor); // manager doubles as CIA for the demo
        GrantRole(manager, rolesByName, AcRoles.AuditCommitteeChair); // …and chairs the AC so one login can drive plan → approve → launch
        GrantRole(auditor, rolesByName, BuiltInRoles.AuditorName);
        GrantRole(auditee, rolesByName, BuiltInRoles.AuditeeName);
        GrantRole(auditee, rolesByName, AcRoles.AuditCommitteeMember); // auditee doubles as an AC member for the demo

        // Provision additional named directory users as data actors. These are realistic Nigerian-bank personas.
        var manager2 = ProvisionActor("chinelo.okafor", "Chinelo", "Okafor", 2001, rolesByName, BuiltInRoles.AuditManagerName);
        var auditor2 = ProvisionActor("emeka.balogun", "Emeka", "Balogun", 2002, rolesByName, BuiltInRoles.AuditorName);
        var auditor3 = ProvisionActor("fatima.suleiman", "Fatima", "Suleiman", 2003, rolesByName, BuiltInRoles.AuditorName);
        var auditee2 = ProvisionActor("tunde.adeyemi", "Tunde", "Adeyemi", 2004, rolesByName, BuiltInRoles.AuditeeName);
        var auditee3 = ProvisionActor("ngozi.eze", "Ngozi", "Eze", 2005, rolesByName, BuiltInRoles.AuditeeName);
        var hrRep = ProvisionActor("bisi.olawale", "Bisi", "Olawale", 2006, rolesByName, SanctionsRoles.HrRepresentative);
        var dc1 = ProvisionActor("kunle.adebayo", "Kunle", "Adebayo", 2007, rolesByName, SanctionsRoles.DisciplinaryCommitteeMember);
        var dc2 = ProvisionActor("amaka.nwosu", "Amaka", "Nwosu", 2008, rolesByName, SanctionsRoles.DisciplinaryCommitteeMember);
        var appeals = ProvisionActor("yakubu.danjuma", "Yakubu", "Danjuma", 2009, rolesByName, SanctionsRoles.AppealsAuthority);
        var acMember2 = ProvisionActor("halima.bello", "Halima", "Bello", 2010, rolesByName, AcRoles.AuditCommitteeMember);
        var acMember3 = ProvisionActor("olumide.johnson", "Olumide", "Johnson", 2011, rolesByName, AcRoles.AuditCommitteeMember);
        var acChair = ProvisionActor("grace.okonkwo", "Grace", "Okonkwo", 2012, rolesByName, AcRoles.AuditCommitteeChair);

        await db.SaveChangesAsync(ct);

        return new DemoUsers(
            AdminId: admin.Id,
            Manager1Id: manager.Id,
            Manager2Id: manager2.Id,
            Auditor1Id: auditor.Id,
            Auditor2Id: auditor2.Id,
            Auditor3Id: auditor3.Id,
            Auditee1Id: auditee.Id,
            Auditee2Id: auditee2.Id,
            Auditee3Id: auditee3.Id,
            HrRepId: hrRep.Id,
            DcMember1Id: dc1.Id,
            DcMember2Id: dc2.Id,
            AppealsId: appeals.Id,
            AcMember1Id: auditee.Id, // the loginable auditee also holds AC Member
            AcMember2Id: acMember2.Id,
            AcMember3Id: acMember3.Id,
            AcChairId: acChair.Id,
            CiaId: manager.Id); // the loginable manager also holds CIA
    }

    private User ProvisionActor(string sam, string first, string last, int sidSuffix, IReadOnlyDictionary<string, Role> rolesByName, string roleName)
    {
        var user = User.ProvisionFromDirectory(
            sam, $"{sam}@auditx.local", $"S-1-5-21-AUDITX-{sidSuffix}", $"{sam}@auditx.local", first, last, $"{first} {last}");
        db.Users.Add(user);
        GrantRole(user, rolesByName, roleName);
        return user;
    }

    private void GrantRole(User user, IReadOnlyDictionary<string, Role> rolesByName, string roleName)
    {
        if (!rolesByName.TryGetValue(roleName, out var role))
        {
            return;
        }

        db.UserRoles.Add(UserRole.Grant(user.Id, role.Id));
        user.MarkActiveOnFirstRole();
    }

    // ---------------------------------------------------------------------------------------------------------
    // M2 — Templates. Three PUBLISHED checklist templates, each with sections + items, published via the domain
    // Publish() factory (which snapshots items into an immutable TemplateVersion, exactly as the API publish flow).
    // ---------------------------------------------------------------------------------------------------------
    private sealed record DemoTemplates(Template BranchOps, Template ItGeneralControls, Template CreditRisk);

    private async Task<DemoTemplates> SeedTemplatesAsync(CancellationToken ct)
    {
        var branch = BuildTemplate(
            "Branch Operations Audit", "branch", "Standard operational audit for retail branches.",
            [
                ("Cash & Teller", "Is cash counted and reconciled daily by two officers?", true),
                ("Cash & Teller", "Are vault access logs complete and dual-controlled?", true),
                ("Cash & Teller", "Are teller till limits enforced and exceptions escalated?", true),
                ("Account Opening", "Is KYC documentation complete for new accounts sampled?", true),
                ("Account Opening", "Are dormant accounts flagged and reactivations authorised?", false),
                ("Physical Security", "Are CCTV and alarm systems tested and operational?", true),
                ("Physical Security", "Is after-hours access restricted and logged?", false),
            ]);

        var itgc = BuildTemplate(
            "IT General Controls Review", "system", "General controls review over core banking and infrastructure.",
            [
                ("Access Management", "Are privileged accounts reviewed quarterly?", true),
                ("Access Management", "Is segregation of duties enforced in the core banking application?", true),
                ("Change Management", "Are production changes approved and tested before release?", true),
                ("Change Management", "Is emergency-change access time-boxed and reviewed?", false),
                ("Backup & Recovery", "Are backups taken daily and restore-tested periodically?", true),
                ("Backup & Recovery", "Is the DR runbook current and last exercised within 12 months?", true),
            ]);

        var credit = BuildTemplate(
            "Credit Risk Review", "process", "Review of credit origination, approval and monitoring.",
            [
                ("Origination", "Are credit appraisals documented and independently reviewed?", true),
                ("Origination", "Is collateral valuation current and within policy limits?", true),
                ("Approval", "Are approvals within delegated authority limits?", true),
                ("Approval", "Are exceptions to policy escalated and approved by the credit committee?", true),
                ("Monitoring", "Are past-due facilities identified and classified correctly?", true),
                ("Monitoring", "Are provisions computed in line with the ECL model?", false),
            ]);

        db.Templates.AddRange(branch, itgc, credit);
        await db.SaveChangesAsync(ct);
        return new DemoTemplates(branch, itgc, credit);
    }

    private Template BuildTemplate(string name, string auditType, string description, (string Section, string Prompt, bool Required)[] items)
    {
        var template = Template.CreateDraft(name, auditType, description);
        foreach (var section in items.Select(i => i.Section).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            template.AddSection(section);
        }

        foreach (var (section, prompt, required) in items)
        {
            template.AddItem(prompt, referenceNotes: null, ResponseType.PassFailNa, section, required, defaultAssignmentRuleJson: null);
        }

        // Publish snapshots the items into an immutable version (mirrors TemplateFlowTests' publish outcome). The
        // snapshot serializer matches the application's AppJson so an audit can deserialize it at creation time.
        template.Publish(_now, snapshot => AuditX.Application.Common.Json.AppJson.Serialize(snapshot));
        return template;
    }

    // ---------------------------------------------------------------------------------------------------------
    // M3 — Audit universe (hierarchy + risk scores) and one APPROVED annual plan (2026) with plan items.
    // ---------------------------------------------------------------------------------------------------------
    private sealed record DemoEntities(
        Guid LagosBranchId,
        Guid AbujaBranchId,
        Guid PhBranchId,
        Guid CoreBankingId,
        Guid DataCentreId,
        Guid TreasuryId,
        Guid CreditId,
        Guid OperationsId);

    private async Task<(DemoEntities Entities, AnnualPlan Plan)> SeedUniverseAndPlanAsync(DemoUsers users, CancellationToken ct)
    {
        var dimensions = await db.RiskDimensions.Where(d => d.IsActive).ToListAsync(ct);
        var specs = dimensions.Select(d => d.ToSpec()).ToList();

        // Divisions (parents).
        var retail = AuditableEntity.Create("process", "Retail Banking", "Retail banking division.", null, users.Auditee1Id);
        var technology = AuditableEntity.Create("process", "Technology", "Technology division.", null, users.Auditee2Id);
        db.AuditUniverseEntities.AddRange(retail, technology);
        await db.SaveChangesAsync(ct); // persist so child FKs (ParentEntityId) reference committed rows

        // Children + standalone units.
        var lagos = AuditableEntity.Create("branch", "Lagos Main Branch", "Flagship Lagos branch.", retail.Id, users.Auditee1Id);
        var abuja = AuditableEntity.Create("branch", "Abuja Branch", "Abuja regional branch.", retail.Id, users.Auditee2Id);
        var ph = AuditableEntity.Create("branch", "Port Harcourt Branch", "Port Harcourt branch.", retail.Id, users.Auditee3Id);
        var coreBanking = AuditableEntity.Create("system", "Core Banking System", "Flexcube core banking platform.", technology.Id, users.Auditee2Id);
        var dataCentre = AuditableEntity.Create("system", "Primary Data Centre", "Primary data-centre facility.", technology.Id, users.Auditee2Id);
        var treasury = AuditableEntity.Create("process", "Treasury", "Treasury and financial markets.", null, users.Auditee3Id);
        var credit = AuditableEntity.Create("process", "Credit", "Credit risk management.", null, users.Auditee3Id);
        var operations = AuditableEntity.Create("process", "Operations", "Central operations and settlements.", null, users.Auditee1Id);

        ApplyScores(lagos, specs, high: true);
        ApplyScores(abuja, specs, high: false);
        ApplyScores(ph, specs, high: false);
        ApplyScores(coreBanking, specs, high: true);
        ApplyScores(dataCentre, specs, high: true);
        ApplyScores(treasury, specs, high: true);
        ApplyScores(credit, specs, high: true);
        ApplyScores(operations, specs, high: false);

        db.AuditUniverseEntities.AddRange(lagos, abuja, ph, coreBanking, dataCentre, treasury, credit, operations);
        await db.SaveChangesAsync(ct);

        var entities = new DemoEntities(lagos.Id, abuja.Id, ph.Id, coreBanking.Id, dataCentre.Id, treasury.Id, credit.Id, operations.Id);

        // One approved 2026 annual plan with six plan items scheduling audits of the entities above.
        var plan = AnnualPlan.Create("FY2026 Annual Audit Plan", new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31));
        plan.AddItem(lagos.Id, "branch", new DateOnly(2026, 2, 1), new DateOnly(2026, 3, 15), 20m, users.Manager1Id);
        plan.AddItem(abuja.Id, "branch", new DateOnly(2026, 3, 1), new DateOnly(2026, 4, 15), 18m, users.Manager1Id);
        plan.AddItem(coreBanking.Id, "system", new DateOnly(2026, 4, 1), new DateOnly(2026, 5, 20), 25m, users.Manager2Id);
        plan.AddItem(credit.Id, "process", new DateOnly(2026, 5, 1), new DateOnly(2026, 6, 20), 22m, users.Manager2Id);
        plan.AddItem(treasury.Id, "process", new DateOnly(2026, 6, 1), new DateOnly(2026, 7, 20), 20m, users.Manager1Id);
        plan.AddItem(ph.Id, "branch", new DateOnly(2026, 7, 1), new DateOnly(2026, 8, 15), 18m, users.Manager1Id);

        // Drive the plan to Approved (Draft → Submit → RecordDecision(Approved)) so audits can link to its items.
        plan.Submit(_now);
        plan.RecordDecision(AcDecisionOutcome.Approved, "Approved by the audit committee for FY2026.",
            ["Coverage aligns with the risk assessment.", "Prioritise the high-risk core-banking review."], users.AcChairId, _now);

        db.AnnualPlans.Add(plan);
        await db.SaveChangesAsync(ct);

        return (entities, plan);
    }

    private static void ApplyScores(AuditableEntity entity, IReadOnlyList<RiskDimensionSpec> specs, bool high)
    {
        // Score every active dimension (values must fall within each dimension's [ScaleMin, ScaleMax]).
        var inherent = specs.ToDictionary(s => s.Name, s => high ? Math.Min(s.ScaleMax, 4) : Math.Max(s.ScaleMin, 2));
        var residual = specs.ToDictionary(s => s.Name, s => high ? Math.Min(s.ScaleMax, 3) : Math.Max(s.ScaleMin, 2));
        entity.ApplyRiskScores(inherent, residual, specs);
    }

    // ---------------------------------------------------------------------------------------------------------
    // Org hierarchy — a small tree (Head Office → 3 divisions); auditable entities + users roll up to a unit so
    // org-based reporting has data. Audits/findings inherit the org via their auditable-entity link.
    // ---------------------------------------------------------------------------------------------------------
    private async Task SeedOrgUnitsAsync(CancellationToken ct)
    {
        var headOffice = AuditX.Domain.Organization.OrgUnit.Create("Head Office", "HO", null);
        db.OrgUnits.Add(headOffice);
        var retail = AuditX.Domain.Organization.OrgUnit.Create("Retail Banking", "RETAIL", headOffice.Id);
        var cib = AuditX.Domain.Organization.OrgUnit.Create("Corporate & Investment Banking", "CIB", headOffice.Id);
        var tech = AuditX.Domain.Organization.OrgUnit.Create("Technology", "TECH", headOffice.Id);
        db.OrgUnits.AddRange(retail, cib, tech);
        await db.SaveChangesAsync(ct);

        // Assign each auditable entity to a division by its type/name; everything else rolls up to Head Office.
        foreach (var entity in await db.AuditUniverseEntities.ToListAsync(ct))
        {
            var name = entity.Name.ToLowerInvariant();
            var type = entity.EntityType.ToLowerInvariant();
            var unitId =
                type == "branch" || name.Contains("branch") ? retail.Id
                : name.Contains("treasury") || name.Contains("credit") ? cib.Id
                : type == "system" || name.Contains("core banking") || name.Contains("data centre") || name.Contains("it ") ? tech.Id
                : headOffice.Id;
            entity.SetOrgUnit(unitId);
        }

        // Populate the auditors' org affiliation (utilisation-by-org reporting) — spread across the divisions.
        var divisions = new[] { retail.Id, cib.Id, tech.Id };
        var users = await db.Users.IgnoreQueryFilters().ToListAsync(ct);
        for (var i = 0; i < users.Count; i++)
        {
            users[i].SetOrgUnit(divisions[i % divisions.Length]);
        }

        await db.SaveChangesAsync(ct);
    }

    // ---------------------------------------------------------------------------------------------------------
    // M4 — Audits. Seven audits spanning states, each with a team, a checklist copied from a published template,
    // and (for six of them) linked to a plan item. Built via AuditCreationService (the exact API creation path).
    // ---------------------------------------------------------------------------------------------------------
    private sealed record DemoAudits(
        Guid Completed1Id,
        Guid Completed2Id,
        Guid UnderReviewId,
        Guid InProgress1Id,
        Guid InProgress2Id,
        Guid PlannedId,
        Guid DraftId);

    private async Task<DemoAudits> SeedAuditsAsync(DemoUsers users, DemoTemplates templates, AnnualPlan plan, DemoEntities entities, CancellationToken ct)
    {
        var items = plan.Items.OrderBy(i => i.PlannedStartDate).ToList();
        // items: [0]=Lagos branch, [1]=Abuja branch, [2]=Core banking, [3]=Credit, [4]=Treasury, [5]=PH branch

        // 1. Completed — Lagos branch, started 90d ago, completed 20d ago.
        var completed1 = await BuildAuditAsync(
            "Lagos Main Branch Operations Audit FY2026", "branch", templates.BranchOps.Id, items[0].Id,
            users.Manager1Id, users.Auditee1Id, [users.Auditor1Id, users.Auditor2Id],
            startOffsetDays: -90, targetOffsetDays: -20, plan, ct);

        // 2. Completed — Core banking ITGC, started 80d ago, completed 15d ago.
        var completed2 = await BuildAuditAsync(
            "Core Banking IT General Controls Review FY2026", "system", templates.ItGeneralControls.Id, items[2].Id,
            users.Manager2Id, users.Auditee2Id, [users.Auditor2Id, users.Auditor3Id],
            startOffsetDays: -80, targetOffsetDays: -15, plan, ct);

        // 3. Under review — Credit risk, started 40d ago.
        var underReview = await BuildAuditAsync(
            "Credit Risk Review FY2026", "process", templates.CreditRisk.Id, items[3].Id,
            users.Manager2Id, users.Auditee3Id, [users.Auditor1Id, users.Auditor3Id],
            startOffsetDays: -40, targetOffsetDays: 10, plan, ct);

        // 4. In progress — Abuja branch, started 25d ago.
        var inProgress1 = await BuildAuditAsync(
            "Abuja Branch Operations Audit FY2026", "branch", templates.BranchOps.Id, items[1].Id,
            users.Manager1Id, users.Auditee2Id, [users.Auditor2Id],
            startOffsetDays: -25, targetOffsetDays: 20, plan, ct);

        // 5. In progress — Treasury, started 15d ago.
        var inProgress2 = await BuildAuditAsync(
            "Treasury Controls Review FY2026", "process", templates.CreditRisk.Id, items[4].Id,
            users.Manager1Id, users.Auditee3Id, [users.Auditor3Id],
            startOffsetDays: -15, targetOffsetDays: 30, plan, ct);

        // 6. Planned — Port Harcourt branch, starts in 10d (kept in Planned state).
        var planned = await BuildAuditAsync(
            "Port Harcourt Branch Operations Audit FY2026", "branch", templates.BranchOps.Id, items[5].Id,
            users.Manager1Id, users.Auditee3Id, [users.Auditor1Id],
            startOffsetDays: 10, targetOffsetDays: 55, plan, ct, stopAt: AuditLifecycleStage.Planned);

        // 7. Draft — Operations (no plan link), stays in Draft.
        var draft = await BuildAuditAsync(
            "Operations & Settlements Review (Draft)", "process", templates.CreditRisk.Id, planItemId: null,
            users.Manager2Id, users.Auditee1Id, [users.Auditor2Id],
            startOffsetDays: 30, targetOffsetDays: 75, plan, ct, stopAt: AuditLifecycleStage.Draft);

        return new DemoAudits(completed1.Id, completed2.Id, underReview.Id, inProgress1.Id, inProgress2.Id, planned.Id, draft.Id);
    }

    private enum AuditLifecycleStage
    {
        Draft,
        Planned,
        InProgress,
        UnderReview,
        Completed,
    }

    private async Task<Domain.Audits.Audit> BuildAuditAsync(
        string name, string auditType, Guid templateId, Guid? planItemId,
        Guid leadId, Guid auditeeId, IReadOnlyList<Guid> auditorIds,
        int startOffsetDays, int targetOffsetDays, AnnualPlan plan, CancellationToken ct,
        AuditLifecycleStage stopAt = AuditLifecycleStage.Completed)
    {
        var startDate = Today.AddDays(startOffsetDays);
        var targetEnd = Today.AddDays(targetOffsetDays);

        var auditableEntityId = planItemId is { } pid ? plan.Items.FirstOrDefault(i => i.Id == pid)?.EntityId : null;
        var data = new CreateAuditData(
            name, auditType, startDate, targetEnd, ScopeDescription: $"Scope: {name}.",
            templateId, TemplateVersion: null, planItemId, auditableEntityId, leadId, auditeeId, auditorIds);

        var audit = await auditCreationService.BuildAsync(data, createdBy: leadId, _now, ct);
        db.Audits.Add(audit);

        if (planItemId is { } linkItemId && plan.Status == PlanStatus.Approved)
        {
            plan.LinkAuditToItem(linkItemId, audit.Id);
        }

        // Advance the lifecycle to the requested stage. Draft → Plan → Start → (responses recorded later) →
        // SendToReview → Complete. Responses/evidence are seeded in a later pass; here we just move the state.
        if (stopAt >= AuditLifecycleStage.Planned)
        {
            audit.Plan();
        }

        if (stopAt >= AuditLifecycleStage.InProgress)
        {
            audit.Start();
        }

        await db.SaveChangesAsync(ct);
        return audit;
    }

    // ---------------------------------------------------------------------------------------------------------
    // M5 — Responses + evidence. Records checklist responses (mix of pass/fail/na) for the in-progress /
    // under-review / completed audits, and attaches small evidence files (via IFileStorage) to a sample of them.
    // The under-review & completed audits get every item finalised; completing then drives them to their state.
    // ---------------------------------------------------------------------------------------------------------
    private async Task SeedResponsesAndEvidenceAsync(DemoAudits audits, DemoUsers users, CancellationToken ct)
    {
        // Fully respond + complete the two completed audits.
        await RespondAndAdvanceAsync(audits.Completed1Id, users.Auditor1Id, finaliseAll: true, complete: true, evidenceCount: 3, ct);
        await RespondAndAdvanceAsync(audits.Completed2Id, users.Auditor2Id, finaliseAll: true, complete: true, evidenceCount: 3, ct);

        // Fully respond the under-review audit (finalising the last required item auto-transitions it to UnderReview)
        // but do NOT complete it — it stays UnderReview.
        await RespondAndAdvanceAsync(audits.UnderReviewId, users.Auditor1Id, finaliseAll: true, complete: false, evidenceCount: 2, ct);

        // Partially respond the two in-progress audits (they stay InProgress with some open items).
        await RespondAndAdvanceAsync(audits.InProgress1Id, users.Auditor2Id, finaliseAll: false, complete: false, evidenceCount: 1, ct);
        await RespondAndAdvanceAsync(audits.InProgress2Id, users.Auditor3Id, finaliseAll: false, complete: false, evidenceCount: 1, ct);
    }

    private async Task RespondAndAdvanceAsync(Guid auditId, Guid actorId, bool finaliseAll, bool complete, int evidenceCount, CancellationToken ct)
    {
        var audit = await db.Audits
            .Include(a => a.ChecklistItems)
            .Include(a => a.Responses)
            .FirstAsync(a => a.Id == auditId, ct);

        var items = audit.ChecklistItems.OrderBy(i => i.OrderIndex).ToList();

        // When not finalising all, leave the last two items unanswered so the audit legitimately stays InProgress.
        var count = finaliseAll ? items.Count : Math.Max(1, items.Count - 2);

        var respondedItemIds = new List<Guid>();
        for (var index = 0; index < count; index++)
        {
            var item = items[index];
            // Deterministic spread: every 4th required item Fails (feeds M6 exceptions), one N/A, rest Pass.
            ResponseVerdict verdict;
            string? comment;
            if (index % 4 == 3)
            {
                verdict = ResponseVerdict.Fail;
                comment = "Control gap identified during fieldwork; see working papers.";
            }
            else if (index % 5 == 2)
            {
                verdict = ResponseVerdict.Na;
                comment = "Not applicable to this branch's operating model.";
            }
            else
            {
                verdict = ResponseVerdict.Pass;
                comment = null;
            }

            audit.RecordResponse(item.Id, verdict, comment, isDraft: false, actorId, requireCommentOnPass: false, _now);
            respondedItemIds.Add(item.Id);
        }

        await db.SaveChangesAsync(ct);

        // Attach a few small evidence files to the first responded items (via IFileStorage; SHA-256 computed).
        var evidenceTargets = respondedItemIds.Take(evidenceCount).ToList();
        var i = 0;
        foreach (var itemId in evidenceTargets)
        {
            var response = await db.ChecklistResponses.FirstOrDefaultAsync(r => r.AuditId == auditId && r.ChecklistItemId == itemId, ct);
            if (response is null)
            {
                continue;
            }

            var content = Encoding.UTF8.GetBytes($"Demo evidence for audit {auditId} item {itemId} (#{++i}).");
            var sha256 = Convert.ToHexStringLower(SHA256.HashData(content));
            var key = $"audits/{auditId}/responses/{response.Id}/{Guid.NewGuid():N}.txt";
            var storagePath = await storage.SaveAsync(key, content, ct);

            var file = EvidenceFile.Create(
                auditId, EvidenceContextType.Response, response.Id, storagePath,
                $"working-paper-{i}.txt", "text/plain", content.LongLength, sha256, response.ResponderUserId, _now);
            db.EvidenceFiles.Add(file);
        }

        await db.SaveChangesAsync(ct);

        // Complete the audit if requested. Once all items are finalised the domain auto-moved it to UnderReview;
        // Complete() moves UnderReview → Completed and stamps the actual end date + MarkAudited on the entity.
        if (complete)
        {
            var reloaded = await db.Audits.Include(a => a.ChecklistItems).FirstAsync(a => a.Id == auditId, ct);
            if (reloaded.Status == AuditStatus.UnderReview)
            {
                reloaded.Complete(Today);
                await db.SaveChangesAsync(ct);
            }
        }
    }

    // ---------------------------------------------------------------------------------------------------------
    // M6 — Exceptions + MAP. Raise exceptions off FAILED checklist items across the lifecycle, ensuring ≥3 CLOSED
    // exceptions share the same (auditable entity, category) so the recurrence scan produces a cluster.
    // ---------------------------------------------------------------------------------------------------------
    private async Task<IReadOnlyList<AuditException>> SeedExceptionsAndMapsAsync(
        DemoAudits audits, DemoEntities entities, DemoUsers users, CancellationToken ct)
    {
        var created = new List<AuditException>();

        // The recurrence cluster: three CLOSED cash-handling exceptions on the Lagos branch entity (same entity +
        // category), all closed within the recurrence window. Drives RecurrenceClusterService to detect a cluster.
        for (var n = 0; n < 3; n++)
        {
            var ex = await RaiseExceptionAsync(
                audits.Completed1Id, entities.LagosBranchId, "Cash reconciliation not performed daily",
                ExceptionSeverity.High, "cash_handling", users.Auditee1Id, users.Auditor1Id,
                raisedOffsetDays: -70 + (n * 10), ct);
            await DriveExceptionToClosedAsync(ex, users, closedOffsetDays: -30 + (n * 5), ct);
            created.Add(ex);
        }

        // A fourth closed cash-handling exception on the same entity (reinforces the cluster; will be a recurrence).
        var ex4 = await RaiseExceptionAsync(
            audits.Completed1Id, entities.LagosBranchId, "Vault dual-control not evidenced",
            ExceptionSeverity.Medium, "cash_handling", users.Auditee1Id, users.Auditor1Id, raisedOffsetDays: -25, ct);
        await DriveExceptionToClosedAsync(ex4, users, closedOffsetDays: -10, ct);
        created.Add(ex4);

        // A CLOSED access-management exception on the core-banking entity (a second, distinct closed finding).
        var exItgc = await RaiseExceptionAsync(
            audits.Completed2Id, entities.CoreBankingId, "Privileged access not reviewed quarterly",
            ExceptionSeverity.High, "access_management", users.Auditee2Id, users.Auditor2Id, raisedOffsetDays: -60, ct);
        await DriveExceptionToClosedAsync(exItgc, users, closedOffsetDays: -12, ct);
        created.Add(exItgc);

        // Under-review audit: a PENDING-CLOSURE exception (Critical → will sit on CIA hold) and a MAP-APPROVED one.
        var exPending = await RaiseExceptionAsync(
            audits.UnderReviewId, entities.CreditId, "Credit appraisals lack independent review",
            ExceptionSeverity.Critical, "credit_process", users.Auditee3Id, users.Auditor1Id, raisedOffsetDays: -30, ct);
        await DriveExceptionToPendingClosureAsync(exPending, users, ct); // Critical → PendingClosure + CiaPending
        created.Add(exPending);

        var exApproved = await RaiseExceptionAsync(
            audits.UnderReviewId, entities.CreditId, "Collateral valuations outdated",
            ExceptionSeverity.High, "credit_process", users.Auditee3Id, users.Auditor3Id, raisedOffsetDays: -28, ct);
        await SubmitAndApproveMapAsync(exApproved, users, ct); // stops at MapApproved
        created.Add(exApproved);

        // In-progress audit: a MAP-SUBMITTED exception and an OPEN one (no MAP yet).
        var exMapSubmitted = await RaiseExceptionAsync(
            audits.InProgress1Id, entities.AbujaBranchId, "Teller till limits exceeded without escalation",
            ExceptionSeverity.Medium, "cash_handling", users.Auditee2Id, users.Auditor2Id, raisedOffsetDays: -20, ct);
        await SubmitMapAsync(exMapSubmitted, users, ct); // stops at MapSubmitted
        created.Add(exMapSubmitted);

        var exOpen = await RaiseExceptionAsync(
            audits.InProgress1Id, entities.AbujaBranchId, "Dormant accounts not flagged",
            ExceptionSeverity.Low, "account_management", users.Auditee2Id, users.Auditor2Id, raisedOffsetDays: -18, ct);
        created.Add(exOpen); // stays Open

        // Treasury in-progress: one more OPEN exception for portfolio spread.
        var exOpen2 = await RaiseExceptionAsync(
            audits.InProgress2Id, entities.TreasuryId, "Position limits monitored manually",
            ExceptionSeverity.Medium, "treasury_process", users.Auditee3Id, users.Auditor3Id, raisedOffsetDays: -10, ct);
        created.Add(exOpen2);

        // Quantify financial exposure on the material findings (feeds $-exposure reporting; NGN base currency).
        exPending.SetFinancialImpact(45_000_000m, "NGN");
        exApproved.SetFinancialImpact(12_500_000m, "NGN");
        exItgc.SetFinancialImpact(8_000_000m, "NGN");
        exOpen2.SetFinancialImpact(30_000_000m, "NGN");
        await db.SaveChangesAsync(ct);

        return created;
    }

    private async Task<AuditException> RaiseExceptionAsync(
        Guid auditId, Guid auditableEntityId, string title, ExceptionSeverity severity, string category,
        Guid ownerUserId, Guid raisedBy, int raisedOffsetDays, CancellationToken ct)
    {
        // Ensure the exception is anchored to a FAILED, finalised checklist item (mirrors the raise gate). Fall back
        // to the first item if no failed response exists on this audit yet.
        var audit = await db.Audits.Include(a => a.ChecklistItems).Include(a => a.Responses).FirstAsync(a => a.Id == auditId, ct);
        var failedItemId = audit.Responses
            .Where(r => !r.IsDraft && r.Verdict == ResponseVerdict.Fail)
            .Select(r => r.ChecklistItemId)
            .FirstOrDefault();
        var checklistItemId = failedItemId != Guid.Empty ? failedItemId : audit.ChecklistItems.OrderBy(i => i.OrderIndex).First().Id;

        var raisedAt = _now.AddDays(raisedOffsetDays);
        var targetDate = DateOnly.FromDateTime(raisedAt.UtcDateTime).AddDays(TargetDaysFor(severity));

        // Spread demo findings across a few root-cause categories (deterministic) so the pareto has shape.
        var rootCauses = new[] { "process_gap", "control_not_operating", "human_error", "system_limitation", "policy_gap" };
        var rootCauseCategory = rootCauses[Math.Abs(raisedOffsetDays) % rootCauses.Length];

        var exception = AuditException.Raise(
            auditId, checklistItemId, auditableEntityId, title, severity,
            rootCause: "Root cause established during fieldwork.", recommendation: "Implement the recommended control.",
            category, rootCauseCategory, ownerUserId, raisedBy, targetDate, targetDateOverridden: false, overrideRationale: null,
            isRecurrence: false, recurrenceOfExceptionId: null, configurationVersionsJson: null, raisedAt);

        db.Exceptions.Add(exception);
        audit.SetItemException(checklistItemId, true);
        await db.SaveChangesAsync(ct);
        return exception;
    }

    // Reproduces the pre-M12 hardcoded target-day defaults (Critical 14 / High 30 / Medium 45 / Low 60).
    private static int TargetDaysFor(ExceptionSeverity severity) => severity switch
    {
        ExceptionSeverity.Critical => 14,
        ExceptionSeverity.High => 30,
        ExceptionSeverity.Medium => 45,
        _ => 60,
    };

    private async Task SubmitMapAsync(AuditException exception, DemoUsers users, CancellationToken ct)
    {
        // A remediation action cannot be due after the exception's target date, so clamp to the target date.
        exception.AddMapAction("Introduce dual control and evidence daily reconciliation.", exception.OwnerUserId, exception.TargetDate, "screenshot");
        exception.AddMapAction("Retrain branch staff on the control procedure.", exception.OwnerUserId, exception.TargetDate, "attendance_register");
        exception.SubmitMap(exception.OwnerUserId, _now);
        await db.SaveChangesAsync(ct);
    }

    private async Task SubmitAndApproveMapAsync(AuditException exception, DemoUsers users, CancellationToken ct)
    {
        await SubmitMapAsync(exception, users, ct);
        exception.ApproveMap(users.Manager1Id, _now);
        await db.SaveChangesAsync(ct);
    }

    private async Task DriveExceptionToPendingClosureAsync(AuditException exception, DemoUsers users, CancellationToken ct)
    {
        await SubmitAndApproveMapAsync(exception, users, ct);
        foreach (var action in exception.MapActions.ToList())
        {
            exception.MarkMapActionComplete(action.Id, requireEvidence: false, hasEvidence: true, users.Auditee1Id, _now);
        }

        exception.MarkMapComplete(users.Auditor1Id);
        exception.Close("All remediation actions completed and evidenced.", users.Manager1Id, _now); // Critical → CiaPending
        await db.SaveChangesAsync(ct);
    }

    private async Task DriveExceptionToClosedAsync(AuditException exception, DemoUsers users, int closedOffsetDays, CancellationToken ct)
    {
        await SubmitAndApproveMapAsync(exception, users, ct);
        foreach (var action in exception.MapActions.ToList())
        {
            exception.MarkMapActionComplete(action.Id, requireEvidence: false, hasEvidence: true, exception.OwnerUserId, _now);
        }

        exception.MarkMapComplete(users.Auditor1Id);

        var closedAt = _now.AddDays(closedOffsetDays);
        // Non-critical closes straight to Closed; a Critical would go on CIA hold (handled elsewhere).
        exception.Close("Remediation verified and closed.", users.Manager1Id, closedAt);
        if (exception.Status == ExceptionStatus.PendingClosure && exception.CiaPending)
        {
            exception.CiaCountersign(users.CiaId, closedAt);
        }

        await db.SaveChangesAsync(ct);
    }

    // ---------------------------------------------------------------------------------------------------------
    // M7 — Sanctions. Four cases triggered off exceptions, in varied states (drafted, submitted, HR outcome,
    // DC decision, appealed). Decoupled from the exception (keys copied at trigger).
    // ---------------------------------------------------------------------------------------------------------
    private async Task SeedSanctionsAsync(IReadOnlyList<AuditException> exceptions, DemoUsers users, CancellationToken ct)
    {
        var closed = exceptions.Where(e => e.Status == ExceptionStatus.Closed).Take(4).ToList();
        if (closed.Count < 4)
        {
            // Fall back to whatever exceptions exist; the demo still populates the sanctions screens.
            closed = exceptions.Take(4).ToList();
        }

        // Case 1 — recommendation drafted only.
        var c1 = SanctionsCase.Trigger(closed[0].Id, users.Auditee1Id, closed[0].Category, closed[0].Severity, closed[0].IsRecurrence, users.Manager1Id, _now);
        c1.RecordRecommendation("Written warning to the branch operations officer.", gridVersion: 1,
            gridRange: "Written warning to two-week suspension", withinRange: true, deviationReason: null, users.Manager1Id, _now);
        db.SanctionsCases.Add(c1);

        // Case 2 — recommendation submitted (awaiting HR).
        var c2 = SanctionsCase.Trigger(closed[1].Id, users.Auditee1Id, closed[1].Category, closed[1].Severity, closed[1].IsRecurrence, users.Manager1Id, _now);
        c2.RecordRecommendation("Two-week suspension for repeated cash-handling breaches.", gridVersion: 1,
            gridRange: "Written warning to two-week suspension", withinRange: true, deviationReason: null, users.Manager1Id, _now);
        c2.SubmitRecommendation(users.Manager1Id, _now);
        db.SanctionsCases.Add(c2);

        // Case 3 — HR outcome recorded (imposed).
        var c3 = SanctionsCase.Trigger(closed[2].Id, users.Auditee2Id, closed[2].Category, closed[2].Severity, closed[2].IsRecurrence, users.Manager2Id, _now);
        c3.RecordRecommendation("Final written warning for the IT access-review lapse.", gridVersion: 1,
            gridRange: "Final written warning to dismissal", withinRange: true, deviationReason: null, users.Manager2Id, _now);
        c3.SubmitRecommendation(users.Manager2Id, _now);
        c3.RecordHrOutcome(HrOutcomeType.Imposed, "{\"outcome\":\"final_written_warning\",\"notes\":\"Imposed by HR after review.\"}", users.HrRepId, _now);
        db.SanctionsCases.Add(c3);

        // Case 4 — referred to DC → DC decision recorded → appealed.
        var c4 = SanctionsCase.Trigger(closed[3].Id, users.Auditee3Id, closed[3].Category, closed[3].Severity, closed[3].IsRecurrence, users.Manager2Id, _now);
        c4.RecordRecommendation("Dismissal recommended for the critical credit-process failure.", gridVersion: 1,
            gridRange: "Final written warning to dismissal", withinRange: false,
            deviationReason: "Aggravating factors: repeated breach and material loss exposure.", users.Manager2Id, _now);
        c4.SubmitRecommendation(users.Manager2Id, _now);
        c4.ReferToDc("Referred to the disciplinary committee for a contested dismissal recommendation.", users.HrRepId, _now);
        c4.RecordDcDecision(DcDecisionType.Modify, "{\"decision\":\"modify\",\"outcome\":\"suspension_one_month\"}", users.DcMember1Id, _now);
        c4.MarkAppealed();
        db.SanctionsCases.Add(c4);

        await db.SaveChangesAsync(ct);
    }

    // ---------------------------------------------------------------------------------------------------------
    // M8 — Reports. Generate a report for each of the two completed audits via the real ReportGenerationService
    // (which renders + stores + hashes + completes), mirroring ReportFlowTests' nudge. The rows are created +
    // committed first so the (same-scope) service can load them.
    // ---------------------------------------------------------------------------------------------------------
    private async Task SeedReportsAsync(DemoAudits audits, CancellationToken ct)
    {
        var template = await db.ReportTemplates.FirstOrDefaultAsync(t => t.IsActive, ct);
        if (template is null)
        {
            logger.LogWarning("No active report template; skipping demo report generation.");
            return;
        }

        foreach (var auditId in new[] { audits.Completed1Id, audits.Completed2Id })
        {
            var nextVersion = await db.Reports.Where(r => r.AuditId == auditId).Select(r => (int?)r.VersionNumber).MaxAsync(ct) ?? 0;
            var report = Report.Start(
                auditId, nextVersion + 1, template.Id, template.VersionNumber, template.TemplateDefinitionJson,
                ["html"], actorId: Guid.Empty, _now);
            db.Reports.Add(report);
            await db.SaveChangesAsync(ct); // commit so the generation service can load it on the same DbContext

            try
            {
                await reportGeneration.RunAsync(report.Id, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Demo report generation failed for audit {AuditId}; continuing.", auditId);
            }
        }
    }

    // ---------------------------------------------------------------------------------------------------------
    // M9 — Analytics. Run the recurrence scan so the ≥3 closed cash-handling exceptions on the Lagos entity surface
    // as a cluster. Dashboards are already seeded by DbSeeder and compute live.
    // ---------------------------------------------------------------------------------------------------------
    private async Task SeedAnalyticsAsync(CancellationToken ct)
    {
        try
        {
            var detected = await recurrenceScan.ScanAsync(ct);
            await db.SaveChangesAsync(ct); // the job normally saves so the new clusters' events dispatch
            logger.LogInformation("Demo recurrence scan detected {Count} cluster(s).", detected);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Demo recurrence scan failed; continuing.");
        }
    }

    // ---------------------------------------------------------------------------------------------------------
    // M12 — Configuration. Add one extra exception_defaults DRAFT version (pending, not activated) for the demo,
    // so the configuration screen shows an active v1 plus a pending draft v2.
    // ---------------------------------------------------------------------------------------------------------
    private async Task SeedConfigurationDraftAsync(CancellationToken ct)
    {
        var maxVersion = await db.BankConfigurations
            .Where(c => c.Domain == ConfigurationDomains.ExceptionDefaults)
            .Select(c => (int?)c.VersionNumber)
            .MaxAsync(ct) ?? 0;

        // A slightly tighter remediation SLA than the active default, staying valid (positive days, window/threshold sane).
        var definition = new ExceptionDefaultsDefinition(
            CriticalTargetDays: 10, HighTargetDays: 25, MediumTargetDays: 40, LowTargetDays: 55,
            RecurrenceWindowMonths: 18, RecurrenceThreshold: 3);
        var definitionJson = ConfigurationDefinitions.SerializeExceptionDefaults(definition);

        var draft = BankConfiguration.CreateDraft(
            ConfigurationDomains.ExceptionDefaults, maxVersion + 1, definitionJson,
            changeReason: "Proposed tighter remediation SLAs for FY2026 (pending audit-committee adoption).",
            createdBy: Guid.Empty, _now);
        db.BankConfigurations.Add(draft);
        await db.SaveChangesAsync(ct);
    }

    // ---------------------------------------------------------------------------------------------------------
    // M13 — Audit committee. Generate one AC pack via the real AcPackGenerationService, approve it, distribute it
    // to the seeded AC cohort, then add action items (varied states) + a couple of comments.
    // ---------------------------------------------------------------------------------------------------------
    private async Task SeedAuditCommitteeAsync(DemoUsers users, CancellationToken ct)
    {
        var nextVersion = await db.AcPacks.Select(p => (int?)p.VersionNumber).MaxAsync(ct) ?? 0;
        var pack = AcPack.Start(
            nextVersion + 1, new DateOnly(2026, 1, 1), new DateOnly(2026, 6, 30), "H1 2026 Audit Committee Meeting",
            ["html"], users.CiaId, _now);
        db.AcPacks.Add(pack);
        await db.SaveChangesAsync(ct); // commit so the generation service can load it

        try
        {
            await acPackGeneration.RunAsync(pack.Id, ct); // → PendingReview (assembles + renders + hashes)

            var generated = await db.AcPacks.Include(p => p.Distributions).FirstAsync(p => p.Id == pack.Id, ct);
            if (generated.Status == AcPackStatus.PendingReview)
            {
                generated.AddSupplementaryText("Chief Internal Auditor's summary for the H1 2026 committee meeting.");
                generated.Approve(users.CiaId, _now);

                // Distribute to the seeded AC cohort (members + chair).
                foreach (var recipient in new[] { users.AcMember1Id, users.AcMember2Id, users.AcMember3Id, users.AcChairId })
                {
                    generated.RecordDistribution(recipient, users.CiaId, _now);
                }

                await db.SaveChangesAsync(ct);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Demo AC pack generation/approval failed; continuing.");
        }

        // Three AC action items in varied states.
        var itemOpen = AcActionItem.Create("Tighten branch cash controls bank-wide", "Per AC review of the Lagos findings.",
            users.Manager1Id, Today.AddDays(60), users.AcMember1Id);

        var itemInProgress = AcActionItem.Create("Accelerate privileged-access review cadence", "Move to monthly reviews.",
            users.Manager2Id, Today.AddDays(45), users.AcMember2Id);
        itemInProgress.MarkInProgress();

        var itemClosed = AcActionItem.Create("Document the DR runbook refresh", "Confirm the DR exercise evidence.",
            users.Manager2Id, Today.AddDays(-5), users.AcChairId);
        itemClosed.MarkInProgress();
        itemClosed.Close("DR runbook refreshed and exercise evidence attached.", users.Manager2Id, _now);

        db.AcActionItems.AddRange(itemOpen, itemInProgress, itemClosed);
        await db.SaveChangesAsync(ct);

        // A couple of AC comments (against the pack and an action item).
        db.AcComments.Add(AcComment.Create(AcCommentTargetType.Pack, pack.Id,
            "The committee notes the recurring cash-handling weakness and requests a follow-up next quarter.", users.AcChairId, _now));
        db.AcComments.Add(AcComment.Create(AcCommentTargetType.Finding, itemOpen.Id,
            "Please provide a remediation timeline for the branch cash controls.", users.AcMember2Id, _now));
        await db.SaveChangesAsync(ct);
    }

    // ---------------------------------------------------------------------------------------------------------
    // M14 — Integrations. Two integrations (SMTP relay + SIEM) and one webhook subscription. Credentials/secrets
    // are protected via ICredentialProtector (the same DataProtection path the create-integration handler uses).
    // ---------------------------------------------------------------------------------------------------------
    private async Task SeedIntegrationsAsync(CancellationToken ct)
    {
        var smtp = IntegrationConfiguration.Create(
            IntegrationType.Smtp, "Primary SMTP Relay",
            "{\"host\":\"smtp.internal.bank\",\"port\":587,\"senderDomain\":\"auditx.bank\"}",
            credentialProtector.Protect("smtp-service-account-password"), timeoutSeconds: 30);

        var siem = IntegrationConfiguration.Create(
            IntegrationType.Siem, "Enterprise SIEM Feed",
            "{\"endpoint\":\"https://siem.internal.bank/ingest\",\"format\":\"cef\"}",
            encryptedCredentials: null, timeoutSeconds: 60);

        db.Integrations.AddRange(smtp, siem);

        var webhook = WebhookSubscription.Create(
            "https://ops.internal.bank/hooks/auditx",
            [Domain.AuditTrail.AuditEventTypes.ExceptionRaised, Domain.AuditTrail.AuditEventTypes.ReportGenerated],
            credentialProtector.Protect("webhook-hmac-shared-secret"),
            retryPolicyJson: "{\"maxAttempts\":5,\"backoffSeconds\":60}");
        db.WebhookSubscriptions.Add(webhook);

        await db.SaveChangesAsync(ct);
    }

    // ---------------------------------------------------------------------------------------------------------
    // M15 — Administration. Ensure bank settings exist (DbSeeder seeds them) and set explicit resource limits so
    // the admin resource-limits screen shows configured values rather than defaults.
    // ---------------------------------------------------------------------------------------------------------
    private async Task SeedAdministrationAsync(CancellationToken ct)
    {
        var settings = await db.BankSettings.FirstOrDefaultAsync(ct);
        if (settings is not null)
        {
            settings.SetResourceLimits(maxEvidenceFileMb: 50, maxAuditEvidenceGb: 10);
            await db.SaveChangesAsync(ct);
        }
    }
}
