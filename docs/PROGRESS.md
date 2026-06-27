# AuditX Enterprise — On-Premises Edition v2.0 · Progress Report

_Single-tenant internal-audit platform for a bank. ASP.NET Core 10 / EF Core 10 (SQL Server) ·
Angular 21 + Material · Clean Architecture monorepo · 15 modules (M1–M15), ~262 user stories._

**Report date:** 2026-06-27 · **Branch:** `main` · **Latest commit:** `e630e37` (M10 Notifications)

---

## 1. Executive summary

- **9 of 15 modules delivered** end-to-end (Domain → Application → Infrastructure → API → Angular → tests):
  **M1, M2, M3, M4, M5, M6, M10, M14, M15.**
- **1 module next up:** **M11 — Audit Trail** (the last of the core-lifecycle phase). Its emit-only seams
  (domain events, `IAuditRecorder`, append-only trigger) are already in place from earlier modules.
- **5 modules not yet started:** M7 (Sanctions), M8 (Reports), M9 (Analytics), M12 (Configuration),
  M13 (Audit Committee Workspace).
- **Quality bar:** every module passes a build with **0 warnings** (warnings-as-errors), a multi-agent
  **adversarial review** with all confirmed findings fixed, and **Docker-gated integration tests** against
  real SQL Server + Redis.

```
Modules delivered  ███████████████████████░░░░░░░░░░░░░  9 / 15  (60%)
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
| M7 | Sanctions & Disciplinary Grid | ⬜ | ⬜ | ⬜ | Not started |
| M8 | Reports | ⬜ | ⬜ | ⬜ | Not started |
| M9 | Advanced Analytics & Dashboards | ⬜ | ⬜ | ⬜ | Not started |
| M10 | Notifications (email/SMS) | ✅ | ✅ | ✅ | **Done** |
| M11 | Audit Trail & Evidence Integrity | 🟡 seams only | ⬜ | ⬜ | **Next** |
| M12 | Template & Workflow Configuration | ⬜ | ⬜ | ⬜ | Not started |
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
- **M11 Audit Trail** — **next.** Append-only trail already written in-transaction by every module; this
  module adds the query, export, and SIEM-streaming surface.

### Phases 3–4 — Findings-to-close, reporting & governance _(not started)_
- **M7 Sanctions & Disciplinary Grid** — disciplinary case lifecycle fed by findings.
- **M8 Reports** — automated report generation (PDF/DOCX) from live audit data, versioned.
- **M9 Advanced Analytics & Dashboards** — configurable dashboards + Audit Committee analytics pack.
- **M12 Template & Workflow Configuration** — configurable state machines / workflow versioning
  (the configurability seam deferred from M4/M6).
- **M13 Audit Committee Workspace** — AC review workspace and sign-off flow.

---

## 4. Engineering metrics (current)

| Suite | Count | Gate |
|-------|------:|------|
| Backend — Domain unit tests | 96 | every build |
| Backend — Application unit tests | 25 | every build |
| Backend — Infrastructure tests | 14 | Docker (Testcontainers SQL Server) |
| Backend — API integration tests | 24 | Docker (Testcontainers SQL Server + Redis) |
| Frontend — Angular specs | 217 | CI |
| **Build warnings** | **0** | warnings-as-errors |

- **EF migrations:** code-first, snake_case schema, applied + seeded on startup; latest `AddNotifications`.
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

Proceed with **M11 — Audit Trail & Evidence Integrity**: the query, filtering, export (CSV/JSON), and
SIEM-streaming surface over the append-only trail that every prior module already populates, plus the evidence
hash-verification/unflag operations seamed in M5. Same rhythm: spec-extraction → backend slice →
adversarial review → tests → Angular feature → commit.
