# AuditX Enterprise — On-Premises Edition v2.0 · Progress Report

_Single-tenant internal-audit platform for a bank. ASP.NET Core 10 / EF Core 10 (SQL Server) ·
Angular 21 + Material · Clean Architecture monorepo · 15 modules (M1–M15), ~262 user stories._

**Report date:** 2026-06-27 · **Branch:** `main` · **Latest commit:** M11 Audit Trail (Phase 2 complete)

---

## 1. Executive summary

- **14 of 15 modules delivered** end-to-end (Domain → Application → Infrastructure → API → Angular → tests):
  **M1, M2, M3, M4, M5, M6, M7, M8, M9, M10, M11, M12, M14, M15.**
- **Phases 1 and 2 are complete**, plus **M7 (Sanctions)**, **M8 (Reports)**, **M9 (Analytics & Dashboards)** and
  **M12 (Configuration)** from the findings-to-close / reporting / governance phase.
- **1 module not yet started:** M13 (Audit Committee Workspace).
- **Quality bar:** every module passes a build with **0 warnings** (warnings-as-errors), a multi-agent
  **adversarial review** with all confirmed findings fixed, and **Docker-gated integration tests** against
  real SQL Server + Redis.

```
Modules delivered  ████████████████████████████████████░  14 / 15  (93%)
```

---

## 2. Module status

| # | Module | Backend | Frontend | Reviewed | Status |
|---|--------|:------:|:--------:|:--------:|--------|
| M1 | Identity, Roles & Permissions (+ Maker-Checker, Delegation) | ✅ | ✅ | ✅ | **Done** |
| M2 | Template Library | ✅ | ✅ | ✅ | **Done** |
| M3 | Audit Universe & Annual Planning | ✅ | ✅ | ✅ | **Done** |
| M4 | Audit Lifecycle | ✅ | ✅ | ✅ | **Done** |
| M5 | Checklist Execution (Responses & Evidence) | ✅ | ✅ | ✅ | **Done** |
| M6 | Exceptions & Management Action Plans (MAP) | ✅ | ✅ | ✅ | **Done** |
| M7 | Sanctions & Disciplinary Grid | ✅ | ✅ | ✅ | **Done** |
| M8 | Reports | ✅ | ✅ | ✅ | **Done** |
| M9 | Advanced Analytics & Dashboards | ✅ | ✅ | ✅ | **Done** |
| M10 | Notifications (email/SMS) | ✅ | ✅ | ✅ | **Done** |
| M11 | Audit Trail & Evidence Integrity | ✅ | ✅ | ✅ | **Done** |
| M12 | Template & Workflow Configuration | ✅ | ✅ | ✅ | **Done** |
| M13 | Audit Committee Workspace | ⬜ | ⬜ | ⬜ | Not started |
| M14 | Integrations & Webhooks | ✅ | ✅ | ✅ | **Done** |
| M15 | Administration | ✅ | ✅ | ✅ | **Done** |

Legend: ✅ complete · 🟡 partial/seams · ⬜ not started

---

## 3. Delivery phases

### Phase 1 — Foundation & platform _(complete)_
Monorepo, Clean Architecture solution, cross-cutting concerns (envelope `{data,metadata}` + RFC 7807,
cursor pagination, request correlation, permission authorization, domain-event dispatch, append-only audit
trail), Docker stack, CI.
- **M1 Identity** — AD/LDAPS + Kerberos auth (no stored passwords), JWT session cookie, JIT provisioning,
  built-in + custom roles with hierarchical inheritance (cycle-safe), scoped permissions, maker-checker
  gates, time-bounded delegation. `0fbb905`
- **M2 Template Library** — authoring, versioning, publication (maker-checker gated), lifecycle. `bc0a1db`, `499f301`
- **M14 Integrations & Webhooks** — encrypted credentials, HMAC-signed webhooks with backoff/dead-letter,
  SIEM export seam. `dbf68c1`, `828e10a`
- **M15 Administration** — bank settings & limits, bulk users/CSV, support channel, signed releases,
  restore drills, system health. `dbf68c1`, `828e10a`

### Phase 2 — Core audit lifecycle _(in progress — 5 of 6 done)_
- **M3 Audit Universe & Planning** — auditable-entity hierarchy, risk scoring, annual plan + AC approval,
  coverage analytics. `8214c5b`, `e88bf2c`
- **M4 Audit Lifecycle** — engagement state machine, team invariants, checklist copied from published
  templates, plan-link wiring, auto-start job. `7ac31b5`, `9de936d`
- **M5 Checklist Execution** — responses (draft/finalise, comment rules), evidence (SHA-256 integrity,
  MIME allow-list, size limits, path-traversal-safe storage), audit-scoped in-handler. `968f860`, `cfe183e`
- **M6 Exceptions & MAP** — 7-state exception lifecycle, MAP submit/approve (maker-checker) /reject,
  CIA countersign for critical closure, raise-from-Fail, remediation evidence. `8e3eff8`, `29c494e`, `502947e`
- **M10 Notifications** — event-driven email/SMS dispatch (rules, templates, retry/dead-letter), admin
  config + per-user preferences. `e630e37`
- **M11 Audit Trail & Evidence Integrity** — general filtered + keyset-cursor query over the append-only
  trail, per-object history, CSV export (SHA-256 integrity hash, PII-safe), self-auditing reads, and the
  M5-deferred evidence unflag/integrity admin. _(Retention enforcement, evidence sampling, SIEM streaming,
  PDF/async export deferred to a later operational pass — see `m11_blueprint.md`.)_

### Phases 3–4 — Findings-to-close, reporting & governance _(in progress — M7 done)_
- **M7 Sanctions & Disciplinary Grid** — disciplinary-case lifecycle fed by exceptions (decoupled from the
  exception lifecycle); versioned sanctions grid with maker-checker activation, recommendation/HR-outcome/
  committee/appeal flow, subject-confidentiality masking, and an evidence dossier. _(HTML dossier — a vetted
  PDF renderer is a later swap; HRIS transmission, Redis grid cache, and the consistency-analytics surface
  deferred — see `m7_blueprint.md`.)_
- **M8 Reports** — versioned, immutable, hash-sealed audit reports generated asynchronously from live M4/M5/M6
  data; canonical **HTML + DOCX** output, download-time integrity verification, and direct email distribution.
  _(Native PDF deferred behind the renderer port; distribution lists / manager placeholder-editing deferred — see
  `m8_blueprint.md`.)_
- **M9 Advanced Analytics & Dashboards** — six seeded, permission-gated dashboards rendering live KPI projections
  (function performance, exception portfolio, coverage, sanctions consistency, recurrence clusters, audit committee),
  a generic configurable widget model, per-audit-lead performance scorecards (with self-coverage suppression), and a
  daily recurrence-cluster detection job that notifies via M10. Sanctions-subject identity is physically omitted from
  every analytics projection. _(Ad-hoc query engine, AC-pack export artefact, predictive indicators and Redis caching
  deferred — see `m9_blueprint.md`.)_
- **M12 Template & Workflow Configuration** — a generic **versioned bank-config store** (draft → activate → rollback,
  one-active-per-domain, change-reason-gated, **maker-checker on activation**, audited with before/after) that turns the
  previously-hardcoded exception SLAs (remediation target-days per severity) and recurrence window/threshold into
  bank-editable config, **snapshotting** the active version that produced each exception's target. Seeds today's
  hardcoded values as v1 so upgrade behaviour is unchanged. _(Escalation rules + hourly evaluator, the visual
  state-machine editor / configurable workflow graphs, preview, bulk import/export, drift detection and taxonomy
  versioning deferred — see `m12_blueprint.md`.)_
- **M13 Audit Committee Workspace** — AC review workspace and sign-off flow.

---

## 4. Engineering metrics (current)

| Suite | Count | Gate |
|-------|------:|------|
| Backend — Domain unit tests | 141 | every build |
| Backend — Application unit tests | 51 | every build |
| Backend — Infrastructure tests | 14 | Docker (Testcontainers SQL Server) |
| Backend — API integration tests | 52 | Docker (Testcontainers SQL Server, collections serialized) |
| Frontend — Angular specs | 323 | CI |
| **Build warnings** | **0** | warnings-as-errors |

- **EF migrations:** code-first, snake_case schema, applied + seeded on startup; latest `AddConfiguration`.
- **Review discipline:** each Phase-2 module ran a multi-dimension adversarial-review workflow
  (correctness / persistence / security / wiring) with per-finding verification; M10 alone fixed 18 findings.

---

## 5. Key architecture decisions

- **API contract:** `{ data, metadata }` success envelope + RFC 7807 `problem+json` errors; 422 for validation.
- **Auth:** Active Directory (LDAPS lookup + Kerberos/IWA SSO) in production — **no passwords stored**; a
  JWT session cookie is issued after AD auth. A seeded **Development** provider (admin/manager/auditor/auditee,
  `Passw0rd!`) runs locally without a domain.
- **CQRS:** hand-rolled dispatcher + FluentValidation pipeline; explicit mappers. Per ADR-0001 we deliberately
  avoid MediatR / AutoMapper / FluentAssertions (now commercially licensed); tests use xUnit + NSubstitute.
- **Concurrency:** rowversion optimistic concurrency on all mutations; child-collection edits promote the
  aggregate root's rowversion via a save-changes interceptor.
- **Authorization:** `[RequirePermission]` is a global check only — per-audit scope is always enforced
  in-handler.
- **Audit trail:** append-only (SQL trigger), written in the same transaction as the state change.
- **UI:** Angular Material (not Tailwind), standalone components + signals, lazy admin routes behind
  permission guards.

---

## 6. Notable deferrals carried forward

- **M10:** in-app notification centre, digests, quiet-hours, maker-checker-on-rule-changes, business-calendar,
  condition predicates, distribution lists, MassTransit transport, the full 40+ event catalogue, and **real SMS**
  (no phone field on users yet) — all flagged for later. Scriban + MailKit were dropped (NuGet-audit CVEs) in
  favour of a regex template renderer + the built-in SMTP client.
- **M4/M6:** configurable workflows/state machines deferred to **M12**.
- **M5/M11:** evidence unflag + sampling deferred to **M11**.

---

## 7. Next step

**Phases 1–2 are complete; M7, M8, M9 and M12 are delivered.** Remaining: **M13 Audit Committee Workspace** —
plus the operational tail deferred across modules (native PDF rendering for dossiers/reports, the M9 ad-hoc query
engine + AC-pack export artefact + predictive indicators + analytics caching, the M12 escalation engine + visual
workflow editor + bulk config import/export + drift detection, distribution lists, notification digests/quiet-hours,
audit-trail retention enforcement, evidence sampling, SIEM streaming transport, HRIS transmission). Each module
follows the same rhythm: spec-extraction → backend slice → adversarial review → tests → Angular feature → commit.
