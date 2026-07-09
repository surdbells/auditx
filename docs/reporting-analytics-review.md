# Reporting & Analytics Implementation Review — AuditX

*Single-tenant bank internal-audit platform · ASP.NET Core 10 + Angular 21 · EF Core · Modules M1–M15*
*Scope: grounded against the actual domain model (Universe/Planning, Execution/Evidence, Findings/MAP/Compliance, Analytics/Reports infra). Every report is validated against captured fields; needs without backing data are flagged as gaps with a capture location.*

---

## 1. Executive Summary

- **Reporting plumbing is genuinely world-class; the analytical *substrate* is thin.** The M8 Reports subsystem is mature: versioned reports, immutable content with canonical-HTML SHA-256 verify-on-read, 5 always-on formats (HTML/PDF/CSV/XLSX) + DOCX on request, template versioning, RBAC-scoped access, and audit-logged generation/distribution. This is above typical GRC-tool maturity.
- **The differentiator gap is data capture, not rendering.** The platform can render anything, but the domain model captures only a narrow slice of what a bank internal-audit function reports on. Roughly half the "150-report" plan is blocked not by UI work but by **missing entities**.
- **No enterprise risk register exists.** Risk is captured only as (a) `RiskDimension` weight axes and (b) inherent/residual composite scores + per-dimension JSON maps *on `AuditableEntity`*. There is no `Risk` aggregate, no risk owner, no likelihood×impact pair, no treatment/mitigation lifecycle. A true risk heatmap, risk register, and risk trend are all blocked.
- **No time/effort/cost capture anywhere.** The only effort datum in the entire system is `PlanItem.EstimatedEffortDays` (planned, days, plan-level). There is no `TimeEntry`/`Timesheet`, no actual hours, no budget/cost fields. Budget-vs-actual, utilisation, cost-per-audit, and resource-allocation reports are **impossible today**.
- **No controls / compliance / regulation register.** No `Control`, `Regulation`, `Framework`, or `NonConformance` entity exists. Findings link only to a free-text `ChecklistItem` prompt and a free-text `Category`. Compliance-by-regulation and control-effectiveness reports cannot be produced.
- **Execution procedures are untyped.** Interviews, walkthroughs, and sampling are all just generic `AuditChecklistItem` rows (prompt + response). Sampling statistics (population, sample size, method, error-rate projection) and interview/walkthrough coverage cannot be reported.
- **The OrgUnit roll-up dimension is wired to nothing.** `OrgUnit` hierarchy exists and `OrgUnitId` is persisted on `User`, `AuditableEntity`, etc., but **zero** analytics/coverage/risk queries group by it (no FK, no index, no projection). Department/business-unit scorecards — a core board ask — are effectively unbuildable today.
- **Findings analytics are the strong suit.** The `AuditException` + `MapAction` model is a genuine formal register: severity, 7-state lifecycle, aging, financial impact, recurrence clustering, MAP remediation, CIA countersign. Findings register, portfolio, material findings, and trend are all production-ready.
- **Trend exists but comparison analytics do not.** `AnalyticsSnapshot` is a solid append-only daily fact table, but there is no MoM/QoQ/YoY period bucketing, no period-over-period delta, and no forecasting (the `ConfigurePredictive` permission exists but routes are deferred).
- **Automation is partial.** Only the quarterly AC pack and the daily snapshot/recurrence jobs are scheduled. There is no general report scheduler, no saved user views, no shareable links, and successful artefact *downloads* are not audit-logged.

**Net maturity:** rendering/export/security ≈ 8/10; findings analytics ≈ 7/10; risk/controls/compliance/effort analytics ≈ 2/10; org-dimensioned & comparative analytics ≈ 2/10.

---

## 2. Report Inventory

Status legend: **Built** = renderer/query exists today · **Data-ready** = data captured, query/report not yet built · **Partial** = some data present, key fields missing · **Blocked** = required entity/field absent.

| Report Family | Representative Reports | Status | Where it lives / would live | Notes |
|---|---|---|---|---|
| **Executive / Board** | Executive summary, KPI pack, board/AC pack, function performance | **Built** | M8 `ExecutiveSummary`/`KpiPack` standalone kinds; M13 `AcPack` (quarterly auto-gen) | Snapshotted M9 analytics + CIA narrative, hash-sealed. Missing OrgUnit rollups. |
| **Planning** | Annual plan status, planned-vs-completed, plan execution %, behind-schedule | **Built** | M8 `AnnualPlanStatus`; `PlanExecutionQuery` | Calendar/Gantt view **Blocked** (no calendar read model; only raw dates + OrderIndex). |
| **Planning – resource/budget** | Auditor workload, budget-vs-actual, resource allocation, utilisation | **Blocked** | would need Execution/Planning | No actual-effort, no capacity field, no cost fields. `EstimatedEffortDays` not rolled into any report. |
| **Risk** | Risk register, risk heatmap, top risks, risk-by-BU, risk trend, appetite-vs-exposure | **Blocked / Partial** | would need new `Risk` module | Only entity-level composite inherent/residual scores exist. Risk-ranked list & inherent-vs-residual scatter are **Data-ready**; register/heatmap/trend **Blocked**. |
| **Coverage** | Coverage matrix, not-audited-since, high-risk gaps | **Built** | `ICoverageQueryService` (US-M3-021/022/023) | Matrix is entity-type × audit-type. Coverage-by-OrgUnit **Blocked**. |
| **Execution** | In-flight vs completed, counts by status, cycle time, schedule variance, checklist progress, review summary | **Built** | `AnalyticsQueryService`, `AuditQueries`, `ExecutionQueries` | Time-in-state must be reconstructed from the trail (no per-status timestamps). |
| **Execution – procedures** | Interviews conducted, walkthrough coverage, sampling statistics/error-rate | **Blocked** | would need Execution | Everything is a generic checklist row; no typed procedure/sample/interview entity. |
| **Findings** | Findings register, open/closed/overdue, by severity/entity/category, aging, material findings, financial-impact rollup, recurrence | **Built** | M6 `AuditException`; `FindingsRegister`; `ExceptionPortfolioDto`/`MaterialFindingsDto` | Strongest family. By-auditor (`RaisedByUserId`) and by-root-cause **Partial** (not a filter/taxonomy). |
| **Compliance** | Status by regulation/policy/standard/ISO, non-conformance summary/trend, control effectiveness | **Blocked** | would need new Compliance module | No regulation/control/framework entity anywhere. Only free-text `Category` + financial impact. |
| **Corrective Action (MAP)** | Action register, open-vs-complete, CAP progress, action aging | **Built / Partial** | M6 `MapAction`; finding-register export | Only 2 states (Pending/Complete); action-overdue is derivable but not surfaced; no by-owner/by-dept rollup. |
| **Follow-up / Verification** | Follow-up schedule, verification results, re-test outcomes, reopened findings | **Blocked** | would need Findings/Follow-up | No follow-up/verification entity; findings have no Reopen edge (Close is terminal). Recurrence is the only weak proxy. |
| **Evidence** | Per-response register, integrity/quarantine, deletion audit, evidence-by-type, missing/outstanding evidence | **Built / Partial** | M5 `EvidenceFile`; `ListEvidenceForResponse`/`ListFlaggedEvidence` | Missing/pending-evidence **Blocked** (no expected-evidence concept). Evidence-by-type **Blocked** (only raw MIME). Cross-audit register **Data-ready**. |
| **Auditor Performance** | Per-lead scorecards, cycle time, exceptions raised/closed, closure days | **Built / Partial** | `PerformanceScorecardsDto` (gated) | Keys on **lead only**; non-lead auditors/reviewers get no metrics. No hours ⇒ no productivity/utilisation. |
| **Management** | Management response, response timeliness/SLA, auditee acknowledgement | **Blocked** | would need Findings | No per-finding management-response record. Auditee only at audit level. MAP timestamps are a partial proxy. |
| **Operational** | Report/AC-pack workflow status, distribution status, notification dispatch, audit-trail export | **Built / Partial** | `Report.Status`, `AcPack.Status`, `NotificationDispatch`, M11 trail | Delivery confirmation deferred (distribution stays Pending). |
| **Analytics / Cross-cutting** | KPI trend lines, recurrence analytics, sanctions consistency, period comparison, forecasting | **Built / Blocked** | M9 `AnalyticsSnapshot`, `GetMetricTrend`, `RecurrenceCluster`, `SanctionsConsistencyDto` | Trend/recurrence/sanctions **Built**; period comparison (MoM/QoQ/YoY) and forecasting **Blocked**. |

---

## 3. Data Gap Matrix (critical deliverable)

| Report / Need | Required Data Element | Captured? | Source Module | Recommended Module | Recommended Screen / Workflow step | Field Type | Mandatory? | Notes |
|---|---|---|---|---|---|---|---|---|
| Enterprise risk register | Individual `Risk` record (distinct from `AuditableEntity`) | **No** | — (only entity scores) | New **Risk** module | Risk identify → assess step | Aggregate root | Yes | No `Risk` entity exists; grep found no register/treatment/appetite type. |
| Risk register | Risk owner (person accountable for the risk) | **No** | Universe (`OwnerUserId` = entity owner) | Risk | Risk create | Guid (User FK) | Yes | Entity owner ≠ risk owner. |
| Risk heatmap | Inherent likelihood × impact (2-axis) | **No** | Universe (only 1-D composite) | Risk | Assess-inherent step | int × int | Yes | Current dimensions are generic weighted, not a fixed L×I pair. Blocks true heatmap grid. |
| Risk heatmap | Residual likelihood × impact | **No** | — | Risk | Assess-residual step | int × int | Yes | Same as above. |
| Risk treatment / mitigation | Treatment plan + treatment status + review-due date | **No** | — | Risk | Treat step | text / enum / DateOnly | Yes | No mitigation lifecycle exists. Findings MAP is issue-level, not risk-level. |
| Risk aging | Date-raised / target / review-due | **No** | — | Risk | Risk create + review | DateOnly | Yes | No risk lifecycle dates. |
| Risk trend (enterprise) | `risk.*` snapshot metric keys | **No** | Analytics (`AnalyticsMetricKeys` has only function/exceptions/plan) | Analytics | Daily snapshot job | metric rows | Yes | Add `risk.*` keys + capture step. Single-entity history exists via trail only. |
| Risk by business unit | OrgUnit grouping on risk/coverage | **No** | Universe (`OrgUnitId` persisted, no FK/index, joined by 0 queries) | Analytics + Universe | Coverage/risk read model | Guid FK + index | Yes | `OrgUnit` is CRUD-only. Add index + rollup service. |
| Budget-vs-actual / cost | Actual auditor hours/effort | **No** | — (only planned `EstimatedEffortDays`) | New **Execution time-log** | "Log time" step | `TimeEntry`(AuditId,UserId,ChecklistItemId?,Date,Hours,category,billable) | Yes | No `TimeEntry`/`Timesheet` anywhere. Blocks utilisation, cost/productivity, effort-per-item. |
| Budget-vs-actual | Budgeted hours / cost on engagement | **No** | Audits/Planning | Planning / Audits | Plan-item or audit setup | decimal (hours + cost) | Yes | No budget/cost field on `Audit` or `PlanItem`. |
| Auditor workload / capacity | Per-auditor capacity/availability | **No** | Identity (`User.OrgUnitId` doc-comment only) | Identity / Org | User profile | decimal (capacity days) | Yes | Enables planned-load-vs-capacity. Workload query also missing. |
| Auditor workload | Aggregated planned effort by lead | **No** (data present, no query) | Planning (`AssignedLeadUserId`+`EstimatedEffortDays`) | Planning | Workload view | query/DTO | No | Data-ready; needs an aggregation query. |
| Planning calendar / Gantt | Calendar read model over plan items | **No** (raw dates only) | Planning (`PlannedStart/EndDate`, `OrderIndex`) | Planning | Plan calendar view | query/DTO | No | Data-ready; needs a calendar query. |
| Controls register | `Control` entity + control-testing results | **No** | — | New **Controls** module | Control library + test step | entity + result enum | Yes | No `Control` entity. Control testing is only an untyped checklist item. |
| Compliance status | `Regulation`/`Framework`/`Standard` register | **No** | — | New **Compliance** module | Regulation library | entity | Yes | No compliance domain folder exists at all. |
| Compliance mapping | Finding ↔ Control/Regulation link | **No** | Exceptions (`ChecklistItemId` only) | Compliance / Exceptions | Raise-finding step | many-to-many link | Yes | Findings carry no control/regulation code. |
| Non-conformance report | Non-conformance type/classification | **No** | — | Compliance | Raise-finding step | enum/ref-data | Yes | No non-conformance field. |
| Root-cause analytics (Pareto) | Structured root-cause **category** | **No** (free-text only) | Exceptions (`RootCause` string) | Exceptions | Raise-finding step | ref-data list `root_cause_category` | Yes | Text exists but cannot aggregate/trend/pareto. |
| Management response | Response text + respondedBy/At + due date + accept/dispute enum | **No** | Exceptions (MAP timestamps are proxy) | Exceptions | Auditee response step | record | Yes | No formal management-response record; auditee only at audit level. |
| Response timeliness / SLA | Response-due-date + elapsed metric | **No** | — | Exceptions | Response step | DateOnly + derived | Yes | No response SLA fields. |
| Follow-up / verification | `FollowUp`/`Verification` record (scheduledDate, verifier, verifiedAt, result enum) | **No** | — (implicit via MAP `CompletedAt` + evidence gate) | Exceptions / Follow-up | Verification step | entity | Yes | No structured verification; verifier ≠ closer not modelled. |
| Reopened findings | Finding Reopen transition + reopen count | **No** | Exceptions (Close is terminal) | Exceptions | Reopen action | transition + int | Yes | Only `Audit` has Reopen; findings do not. |
| Sampling statistics | Population, sample size, method, items tested, exceptions-in-sample, error-rate | **No** | — (generic checklist row) | Execution | Sampling step | `SamplingRecord` | Yes | No sampling structure; error-rate projection impossible. |
| Interviews conducted | Interviewee, date, topic, attendees | **No** | — (free text) | Execution | Interview step | `Interview` entity | No | Counts/coverage unavailable. |
| Walkthrough coverage | Walkthrough record | **No** | — | Execution | Walkthrough step | entity | No | Untyped free text today. |
| Missing/outstanding evidence | Expected/requested-evidence concept | **No** (evidence only after upload) | Evidence | Execution/Evidence | Requested-evidence checklist | requested-vs-received | Yes | Blocks pending/outstanding-evidence reports. |
| Evidence by business type | Document-type / category taxonomy | **No** (raw MIME only) | Evidence | Evidence | Upload step | ref-data list | No | Business-sense evidence-by-type unavailable. |
| Per-auditor performance | Metrics for non-lead auditors/reviewers | **No** (lead-only) | Audits (`AssignedUserId`, `ResponderUserId` present) | Analytics | Scorecard query | aggregation | No | Data present (item assignment/responder); scorecard keys only on lead. |
| Time-in-state / turnaround | Per-status transition timestamps on `Audit` | **No** (only `ActualEndDate`) | Audits (trail only) | Audits | State transitions | timestamp columns or history table | No | `LastTransitionReason` overwrites; reopen/return counts need trail parsing. |
| Department scorecards | OrgUnit projection into findings/audits KPIs | **No** | Analytics (groups by entity name only) | Analytics | KPI projections | Guid FK + rollup | Yes | Core board ask; currently unbuildable from projections. |
| Period comparison | MoM/QoQ/YoY bucketing + delta | **No** (raw daily series only) | Analytics (`AnalyticsSnapshot`) | Analytics | Comparison query | query | No | Backend computes no comparison. |
| Forecasting | Forecast/projection/target service | **No** (`ConfigurePredictive` deferred) | Analytics | Analytics | Predictive service | service + target entity | No | History exists; no projection. |
| Scheduled/emailed reports | `ReportSchedule` (kind, cadence, recipients, format) | **No** (only AcPack hardcoded quarterly) | Reports | Reports | Schedule-config step | aggregate + Hangfire job | No | No cron/frequency on Report/Template. |
| Saved views | `SavedReportView`/`ReportPreset` (owner, kind, filter JSON, columns) | **No** | Reports (single bank-wide template only) | Reports | "Save view" | entity | No | No per-user saved filters. |
| Shareable links | `ShareLink` (reportId, token, expiresAt, scope) | **No** | Reports | Reports | Share action | entity + token endpoint | No | Distribution is email-only. |
| Report download evidence | `report_downloaded`/`report_exported` audit event | **No** (only hash-mismatch logged) | Reports/Trail | Reports | Download handler | trail event | Yes | Who-exported-what not captured for artefacts. |

---

## 4. Missing Data Capture Recommendations (prioritized)

**P0 — the two additions that unlock the most reports:**

1. **Time/effort capture (`TimeEntry`).** A single new entity `TimeEntry(AuditId, UserId, ChecklistItemId?, Date, Hours decimal, ActivityCategory, Billable bool)` plus a "Log time" step in Execution unlocks: utilisation, cost/productivity per auditor, budget-vs-actual, effort-per-audit, effort-per-checklist-item, and normalises every auditor-performance metric by time. Pair it with **budgeted hours/cost** fields on `Audit` (or `PlanItem`) so actual-vs-budget becomes a subtraction. This is the single highest-leverage gap because it blocks an entire report family and improves three others.

2. **OrgUnit wiring.** The hierarchy already exists; it is simply not joined. Add an **index on `OrgUnitId`** (Universe entities, users, and — via `AuditableEntity` join — findings/audits), a **descendant-rollup service** (aggregate an org node + its subtree), and an **OrgUnit dimension** in `AnalyticsQueryService` + new `*.by_orgunit` snapshot metrics. This is cheap (data is already persisted) and unlocks department/business-unit scorecards, risk-by-BU, and coverage-by-BU — all core board asks.

**P1 — new registers that unlock whole families:**

3. **Risk module.** A `Risk` aggregate + `RiskTreatment` child with the workflow *identify → assess-inherent → treat → assess-residual → review*. Fields: `OwnerUserId`, `InherentLikelihood/Impact int`, `ResidualLikelihood/Impact int`, `TreatmentPlan text`, `TreatmentStatus enum`, `Status enum`, `ReviewDueDate DateOnly`. The explicit L×I split (not the current generic composite) is what enables the true risk heatmap. Add `risk.*` snapshot keys in the same change to get risk trend.

4. **Controls + Compliance registers.** A `Control`/`Framework`/`Regulation` register + a many-to-many **Finding↔Control/Regulation** link captured at raise-finding time, plus a `NonConformanceType`. Add a `ControlTestResult` (effectiveness enum) as a typed sub-record off checklist items. This unlocks the entire Compliance family, which is currently 100% blocked.

**P2 — structured capture that upgrades existing reports from Partial to Built:**

5. **Root-cause taxonomy.** Add a `root_cause_category` reference-data list (mirroring `exception_category`) as a required field on the raise-finding step. Small change; unlocks pareto/trend by root cause.

6. **Management response + follow-up/verification.** A `ManagementResponse` record (text, respondedBy/At, responseDueDate, accepted/disputed enum) and a `FollowUp`/`Verification` entity (scheduledDate, performedBy, verifiedAt, result enum) — plus a **finding Reopen transition + reopen count**. Unlocks management-timeliness and follow-up-effectiveness families.

7. **Typed execution procedures.** `SamplingRecord` (population, sampleSize, method, itemsTested, exceptionsFound), `Interview`, `Walkthrough` as first-class execution steps (or typed sub-records off a checklist item). Unlocks sampling error-rate projection and interview/walkthrough coverage.

8. **Expected-evidence concept.** A requested-vs-received checklist so MISSING/PENDING evidence reports become possible; add a document-type ref-data list for evidence-by-type.

---

## 5. Report-to-Module Mapping

| Report Family | Owning Module (feeds the report) |
|---|---|
| Executive / Board / AC pack | **M13 AC** (pack) + **M9 Analytics** (snapshotted KPIs) + **M8 Reports** (rendering) |
| Planning status & execution | **M3 Planning** (`AnnualPlan`/`PlanItem`) → **M8 Reports** (`AnnualPlanStatus`) |
| Planning resource/budget | *would be* **new Execution time-log** + **M3 Planning** (budget fields) |
| Risk register / heatmap / trend | *would be* **new Risk module** + **M9 Analytics** (`risk.*` keys) |
| Coverage & gaps | **M3 Universe/Coverage** (`ICoverageQueryService`) |
| Execution status / cycle time | **M4/M5 Audits+Execution** + **M9 Analytics** |
| Execution procedures (sampling/interview) | *would be* **new Execution entities** |
| Findings register / portfolio | **M6 Exceptions** → `FindingsRegister`, `ExceptionPortfolioDto` |
| Compliance / controls | *would be* **new Compliance/Controls module** |
| Corrective action (MAP) | **M6 Exceptions** (`MapAction`) |
| Follow-up / verification | *would be* **M6 Exceptions extension** |
| Evidence | **M5 Evidence** (`EvidenceFile`) |
| Auditor performance | **M9 Analytics** (`PerformanceScorecardsDto`) |
| Management response | *would be* **M6 Exceptions extension** |
| Operational (workflow/distribution) | **M8 Reports**, **M10 Notifications**, **M11 Audit Trail** |
| Sanctions consistency | **M7 Sanctions** (`SanctionsConsistencyDto`, Category-dimensioned) |
| Recurrence analytics | **M9 Analytics** (`RecurrenceCluster`) |

---

## 6. Drilldown Map

Legend: ✅ real FK/projection today · ⚠️ persisted but not wired (no index/join/projection) · ❌ entity does not exist.

```
Enterprise (bank)                    [implicit root]
   │  ✅ OrgUnit tree (ParentOrgUnitId, acyclic)
Business Unit / Division (OrgUnit)
   │  ⚠️ OrgUnit → AuditableEntity : AuditableEntity.OrgUnitId persisted, NO FK/index, joined by 0 queries
Department (OrgUnit leaf)
   │  ⚠️ same — OrgUnit roll-up is CRUD-only, absent from all KPIs
Auditable Entity (Universe)
   │  ✅ Audit.AuditableEntityId (direct link, added recently) ; ✅ PlanItem.EntityId
Audit (engagement)
   │  ✅ AuditException.AuditId ; ✅ AuditException.ChecklistItemId (Restrict)
Finding (AuditException)
   │  ✅ EvidenceFile.AuditId + ContextType/ContextId (polymorphic → ChecklistResponse)
   │  ⚠️ Finding→Evidence is via Audit/response context, not a direct Finding FK
Evidence (EvidenceFile)
   │  ✅ MapAction.ExceptionId (child aggregate, cascade)
Corrective Action (MapAction)
   │  ❌ Follow-up / Verification entity does not exist (no scheduledDate/verifier/result)
Follow-up / Verification
   │  ✅ AuditTrailEntry (append-only, TargetObjectType/Id) covers every hop
Audit Trail (M11)
```

**Supported hops (real FKs):** Audit→Entity, Audit→Finding, Finding→ChecklistItem, Finding→MAP, Finding/Audit→Evidence (via audit+context), everything→Audit Trail, Plan→Entity, Plan→Audit.

**Needs wiring (⚠️):** OrgUnit→Entity, OrgUnit→Audit, OrgUnit→Finding, OrgUnit→User rollups — all persisted but not projected. Add index + rollup service.

**Missing entirely (❌):** the Corrective-Action → Follow-up/Verification hop (no entity), and Finding→Reopen (no transition). Risk→Finding and Risk→Control hops don't exist because neither Risk nor Control entities exist.

**Generic drilldown caveat:** there is **no "expand total → underlying rows" service**. Click-through works only where a DTO happens to project an id (`ExceptionId`, `AuditId`, `AuditableEntityId`, cluster `MemberExceptionIds`). Aggregate scalars/percentages have no backing "show records" query.

---

## 7. KPI Catalogue

### Available now (from `IAnalyticsQueryService` + snapshot store)

**Function performance:** AuditsInFlight, AuditsCompleted, PlanItemsTotal/Completed, PlanExecutionPercent, OpenExceptionBacklog, ClosedExceptions, ClosureRatePercent.
**Plan status:** TotalPlans, TotalItems, Planned, InProgress, Completed, Deferred, CompletionPercent.
**Performance scorecards (per audit lead):** AuditsLed/Completed, AvgCycleDays, ExceptionsRaised/Closed, AvgClosureDays.
**Exception portfolio:** open-by-severity, age buckets (0-30/31-60/61-90/90+), by-entity open count + avg closure, avg closure days.
**Material findings:** open Critical/High.
**Sanctions consistency:** grid-adherence % (`WithinGridRange`), deviation, appeal rate — dimensioned by Category (business-unit proxy).
**Recurrence:** clusters by entity+category over window (threshold 3).
**Coverage:** matrix (entity-type × audit-type), not-audited-since, high-residual-risk gaps.
**Trend (via `AnalyticsSnapshot`):** `function.*`, `exceptions.total_open/avg_closure_days/open_by_severity`, `plan.completion_pct/items_*` — trailing-90-day default, arbitrary from/to, severity slices via Dimension.

### High-value KPIs still missing

| KPI | Blocked by |
|---|---|
| Enterprise risk exposure / top-N residual risks / appetite-vs-exposure | No `Risk` entity |
| Risk trend, risk velocity (new vs closed risks) | No `risk.*` snapshot keys |
| Auditor utilisation %, cost-per-audit, effort-per-finding, planned-vs-actual hours | No `TimeEntry`/budget |
| Auditor workload / capacity (forward planned load) | No workload query + no capacity field |
| Department/BU scorecards (findings, coverage, risk by OrgUnit) | OrgUnit not projected |
| Per-non-lead-auditor throughput (items responded, evidence uploaded) | Scorecards lead-only |
| Management-response timeliness / SLA breach % | No response record |
| Corrective-action effectiveness (verified-vs-recurred) | No verification entity |
| Root-cause pareto (% by cause category) | Root cause is free-text |
| Compliance % by regulation, non-conformance rate | No compliance register |
| Sampling error-rate / projected exceptions | No sampling entity |
| Period-over-period deltas (MoM/QoQ/YoY), forecast-vs-target | No comparison/forecast service |
| Evidence completeness % (received vs expected) | No expected-evidence concept |

---

## 8. Visualization Recommendations

Available hand-rolled SVG primitives: **bar, donut, gauge, line.**

| Report Family | Best chart | Available today? |
|---|---|---|
| Executive KPI pack | KPI tiles + gauge + trend line | ✅ gauge/line exist |
| Plan status / execution | Stacked bar (by status) + progress gauge | ✅ bar/gauge |
| Plan calendar / schedule | **Gantt / timeline** | ❌ new SVG component |
| Coverage matrix | **Heatmap grid** (entity-type × audit-type, cell counts) | ❌ new heatmap (data ready) |
| Risk register | Ranked bar (residual score) | ✅ bar (from entity scores) |
| Risk heatmap | **Risk matrix** (5×5 likelihood × impact) | ❌ new SVG + needs L×I data |
| Risk trend | Line | ✅ line — but needs `risk.*` keys |
| Findings by severity | Donut / stacked bar | ✅ |
| Findings aging | Bar (age buckets) | ✅ |
| Findings trend | Line | ✅ |
| Root-cause pareto | **Pareto (bar + cumulative line)** | ⚠️ compose bar+line; needs cause taxonomy |
| Recurrence clusters | Bar / **treemap** (by entity) | ✅ bar; treemap ❌ optional |
| Corrective action (CAP) progress | Stacked bar / gauge | ✅ |
| Auditor performance | Grouped bar + scatter | ✅ bar; scatter ❌ optional |
| Auditor workload | Horizontal bar (load vs capacity) | ✅ bar — needs workload query + capacity |
| Budget-vs-actual | **Bullet / grouped bar** | ⚠️ needs time/budget data |
| Evidence integrity | Table + donut (flagged/clean) | ✅ |
| Department scorecards | Heatmap / small-multiples bar | ❌ heatmap + needs OrgUnit rollup |
| Sanctions consistency | Grouped bar (grid-adherence by category) | ✅ |
| Period comparison | **Grouped/dual-axis bar** or slope chart | ⚠️ needs comparison query |
| Analytics dashboards | Configurable per-widget (Chart/Table/SingleMetric) | ✅ M9 DashboardWidget |

**New SVG components worth building:** heatmap (unlocks coverage matrix + department scorecards immediately, data already exists), risk matrix (blocked on L×I data), gantt/timeline (plan calendar), pareto composition, and optionally treemap/scatter/bullet.

---

## 9. Filter Catalogue

| Filter | Plan wants | Supported today? |
|---|---|---|
| Status | all families | ✅ (audits, findings, plans, reports) |
| Severity | findings/risk | ✅ findings (`Severity`) |
| Category | findings/sanctions | ✅ (`Category`, ref-data) |
| Owner (remediation) | findings/CAP | ✅ (`OwnerUserId`) |
| Raised-by (auditor) | findings | ⚠️ persisted (`RaisedByUserId`) but **not** a filter dimension |
| Auditable entity | findings/coverage | ✅ (`AuditableEntityId`) |
| Audit | findings/evidence | ✅ (`AuditId`) |
| Annual plan | findings/planning | ✅ (`AnnualPlanId`) |
| Raised date range | findings | ✅ (`RaisedFrom/RaisedTo`) |
| Overdue / recurrence | findings | ✅ (`IsOverdue`, `IsRecurrence` derived) |
| Audit type | coverage/execution | ⚠️ free-text string (`AuditType`), not enum/FK — string-value dependent |
| Lead / assignee | performance/workload | ✅ lead (`LeadUserId`); ⚠️ item assignee not aggregated |
| **OrgUnit / department / BU** | scorecards, risk, coverage | ❌ persisted, joined by 0 queries |
| **Risk status / risk owner / L×I** | risk | ❌ no Risk entity |
| **Regulation / control / framework** | compliance | ❌ no entity |
| **Root-cause category** | findings pareto | ❌ free-text only |
| **Date/period granularity (month/qtr/yr)** | trend comparison | ❌ raw daily only |
| **Evidence type** | evidence | ❌ raw MIME only |
| **Time period / effort / cost** | resource | ❌ no time data |
| MAP action status / action-overdue | CAP | ⚠️ derivable (`TargetDate<today & Pending`) but not surfaced as filter |

---

## 10. Export Matrix

| Capability | Status | Notes |
|---|---|---|
| HTML | ✅ Built | Canonical, always produced; SHA-256 hashed |
| PDF | ✅ Built | Server-side via MigraDoc; always produced |
| CSV | ✅ Built | Always produced; dedicated finding-register + trail exports |
| XLSX | ✅ Built | Always produced |
| DOCX | ✅ Built | On request (OpenXmlReportRenderer); AC pack = html+docx |
| Download with integrity check | ✅ Built | `DownloadReportArtefactQuery` verifies SHA-256 on read |
| Email distribution | ✅ Built | To directory users / ad-hoc emails / role→active-members; per-recipient row |
| **Download/export audit logging** | ⚠️ Partial | Generation/distribution logged; successful **download not logged** (only hash-mismatch). Add `report_downloaded`. |
| **Delivery confirmation** | ❌ Missing | `ReportDistribution.Outcome` stays Pending; bounce/read callback deferred |
| **Scheduled reports** | ❌ Missing | Only AcPack (hardcoded quarterly). No `ReportSchedule`, no cron/frequency field |
| **Saved templates / views** | ⚠️ Partial | Single bank-wide `ReportTemplate` (engagement only, one active version). No per-user saved filters/`ReportPreset` |
| **Shareable links** | ❌ Missing | No `ShareLink`/token entity; access is interactive RBAC only |
| **Retention/expiry** | ⚠️ Partial | `Report.RetentionUntil` column exists; expiry job deferred |

---

## 11. Security Considerations

- **RBAC (strong).** Granular permission keys: `GenerateReport`/`ViewReport`/`DistributeReport`/`ConfigureReports`; `ViewAnalytics`/`ConfigureDashboards`/`PerformanceAnalyticsView`/`ConfigurePredictive`/`SensitiveQueryAccess`/`AdHocQueryUse`; AC keys (`ViewAcPacks`/`GenerateAcPack`/`AcMember`/`AcChair`/`CIA`); `ViewAuditTrail`/`ExportAuditTrail`.
- **Resource scoping.** Engagement reports require audit-team membership OR `ManageAudit` scope; standalone reports require `ViewAnalytics` (+`PerformanceAnalyticsView` for scorecards). Dashboards double-gate (dashboard permission + per-widget `TargetRoleId`). Scorecards **self-suppress the caller's own row** unless CIA.
- **Masking / row-level.** Sanctions `SubjectUserId` is **physically omitted from every analytics projection** (FR-M7-010/NFR-SEC-007) — the required subject-identity omission is enforced, aggregation is by Category only. `FindingVisibilityRestriction` hides restricted material-finding detail per-requester on the AC dashboard.
- **Audit logging.** `report_generation_requested/generated/failed/distributed/hash_mismatch` recorded; `dashboard_refreshed` on every dashboard read; trail + finding-register exports audited. **Gap:** artefact *download* not logged — add a `report_downloaded/exported` event to complete who-exported-what evidence.
- **Integrity.** Canonical HTML SHA-256 stored; verify-on-read raises a **Critical `report_hash_mismatch` alert + 500** on tamper. Reports are content-immutable after Complete; AC packs re-seal at approval.
- **When new modules land:** apply the same masking discipline — a Risk register must respect entity-level access; time/effort data is personnel-sensitive (utilisation) and should gate behind a dedicated permission; management-response text may contain sensitive auditee statements.

---

## 12. Performance Recommendations

- **Snapshot/materialization already exists — extend it.** `AnalyticsSnapshot` (idempotent daily, delete-then-insert) is the right pattern. Add `risk.*` and `*.by_orgunit` keys so heavy rollups are precomputed, not live-aggregated.
- **Large registers need cursor pagination.** Findings register and (future) risk/controls registers can grow unbounded. `ListAuditsQuery` already uses cursor paging — apply the same to `SearchExceptionsQuery` result surfaces and any new register query; avoid offset paging on the CSV/XLSX export path (stream instead).
- **Index the roll-up dimension.** `OrgUnitId` has **no index** on `AuditableEntity`; any OrgUnit grouping will table-scan. Add indexes before wiring rollups. The descendant-rollup service should use a recursive CTE or a precomputed closure table for deep hierarchies.
- **Cache point-in-time KPIs.** Live `AnalyticsQueryService` calls recompute on every dashboard read (and `dashboard_refreshed` fires each time). Cache with a short TTL keyed on permission scope, or serve dashboards from the daily snapshot where freshness allows.
- **Export generation is CPU-heavy (5 formats always).** `Report.NormaliseFormats` always produces HTML/PDF/CSV/XLSX. For scheduled/bulk generation, produce lazily or on-download rather than eagerly for every kind.
- **Verify-on-read hashing** re-verifies evidence SHA-256 on every read — fine per-file, but a cross-audit evidence register that reads many files should batch/stream rather than verify synchronously in a list query.

---

## 13. Future Advanced Analytics

- **Forecasting / predictive.** `ConfigurePredictive` permission exists but routes are deferred. Build a forecasting service over the `AnalyticsSnapshot` series (backlog burn-down projection, closure-rate forecast, plan-completion trajectory) + a `Target`/`Forecast` entity for target-vs-actual. Start with simple linear/seasonal projection on existing keys.
- **Period comparison (MoM/QoQ/YoY).** Not present as first-class. Add a `PeriodComparisonQuery(metric, granularity=month|quarter|year, compareTo=priorPeriod|priorYear)` returning current/prior/delta/variance. The daily series already supports arbitrary from/to; the backend just needs bucketing + delta computation.
- **Recurrence → predictive.** `RecurrenceCluster` already detects repeat findings (entity+category, threshold 3). Extend to a predictive signal: entities with rising recurrence velocity flagged as elevated-risk candidates feeding the (future) risk register.
- **Risk trend & velocity.** Once the Risk module + `risk.*` snapshot keys exist, add new-vs-closed risk velocity, residual-risk drift, and treatment-ageing analytics.
- **Auditor productivity analytics.** Once `TimeEntry` exists, layer utilisation trends, cost-per-finding, and capacity-forecasting on the snapshot store.
- **Anomaly detection.** Over the snapshot fact table, flag out-of-band closure-rate drops or backlog spikes for the AC pack narrative.

---

## 14. Prioritized Roadmap

### Must Have (unblock core board/regulatory reporting)
- **Wire OrgUnit rollups** — index `OrgUnitId`, add descendant-rollup service, add OrgUnit dimension to `AnalyticsQueryService` + `*.by_orgunit` snapshot keys. *(Cheap; data already persisted. Unlocks department/BU scorecards, risk-by-BU, coverage-by-BU.)*
- **Time/effort capture (`TimeEntry`) + engagement budget fields.** *(Unlocks the entire resource/budget family + normalises performance metrics. No dependencies.)*
- **Root-cause taxonomy** — `root_cause_category` ref-data list on raise-finding. *(Tiny change; unlocks pareto/trend.)*
- **Report-download audit event** — close the who-exported-what gap. *(Small; compliance-relevant.)*
- **Heatmap SVG component** — unlocks coverage matrix visualization immediately (data ready). *(No data dependency.)*

### Should Have (unblock whole missing families)
- **Risk module** (`Risk` + `RiskTreatment`, L×I axes, lifecycle) + `risk.*` snapshot keys. *(Depends on: snapshot job. Unlocks risk register, heatmap, trend. Risk matrix SVG follows.)*
- **Controls + Compliance registers** + Finding↔Control/Regulation link + non-conformance type. *(Unlocks the fully-blocked Compliance family.)*
- **Management response + Follow-up/Verification + finding Reopen.** *(Depends on: M6 Exceptions. Unlocks management-timeliness and follow-up families.)*
- **Per-auditor (non-lead) scorecards** — aggregate `AssignedUserId`/`ResponderUserId`. *(Data present; query only. Better with `TimeEntry`.)*
- **Auditor workload query + capacity field** + plan calendar/Gantt read model. *(Data mostly present; needs aggregation + Gantt SVG.)*
- **Period-comparison query** (MoM/QoQ/YoY). *(Depends on: snapshot store. Backend bucketing only.)*

### Nice to Have (maturity & automation)
- **Report scheduler** (`ReportSchedule` + recurring Hangfire dispatcher) for Executive/KPI/Findings on a cadence.
- **Saved views / presets** (`SavedReportView`) and **shareable tokenized links** (`ShareLink`).
- **Typed execution procedures** — `SamplingRecord`, `Interview`, `Walkthrough` (unlocks sampling error-rate, interview/walkthrough coverage).
- **Expected-evidence concept** + document-type taxonomy (unlocks missing/outstanding-evidence and evidence-by-type).
- **Forecasting/predictive service** (activates the deferred `ConfigurePredictive` surface) + delivery-confirmation callbacks + retention-expiry job.
- **Generic drilldown service** ("expand total → underlying rows") so every aggregate KPI supports click-through, not only the DTOs that happen to project ids.

**Sequencing note:** OrgUnit wiring, `TimeEntry`, and root-cause taxonomy have **no dependencies** and each unblocks multiple reports — do them first and in parallel. The Risk and Compliance modules are larger and gate their own report families, so they follow. Scheduler/saved-views/share-links are pure UX/automation on top of the already-mature M8 rendering layer and can land last.