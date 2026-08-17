# AuditX — Modules & Submodules

A two-page map of every functional module in the platform and the submodules within it. Module numbers
(M1–M15) reference the AuditX BRD/PRD where they are established; the functional grouping below reflects the
delivered codebase (backend domain areas + API controllers + Angular feature areas).

---

### 1. Identity & Access — *M1*
- **Authentication** — Active Directory (LDAPS lookup + Kerberos/IWA SSO), forms fallback, and a Development
  provider for local use. No stored passwords; JWT session cookies (8h sliding / 24h absolute, Redis denylist).
- **Users** — just-in-time provisioning, activation/deactivation, awaiting-role state, notification preferences.
- **Roles & Permissions** — AuditX-owned roles independent of AD groups; scoped permissions with role
  inheritance (cycle-prevented); built-in role catalogue.
- **Maker-Checker (dual control)** — admin-configurable approval gates; pending-action queue; approve/reject.
- **Delegation** — time-bounded authority delegation with an hourly expiry job.

### 2. Audit Universe & Org Structure — *M2/M3*
- **Auditable entities** — the universe of things that can be audited; bulk import; owner + org-unit links.
- **Entity types** — taxonomy with a default *expected-audits-per-year* frequency (overridable per entity).
- **Organisational units** — hierarchical org tree; used for scoping and department roll-up analytics.
- **Risk scoring** — per-entity risk scores with history, feeding risk-based plan prioritisation.

### 3. Annual Planning — *M3*
- **Annual plans** — period-scoped audit plans; draft → submitted → decided → closed.
- **Plan items** — multi-entity items (one audit launched per entity); effort estimates; assigned lead.
- **Audit-committee routing** — submit / submit-revision / decision; approval by the committee.
- **Post-approval governance** — approved plans lock; material revision re-opens for re-approval; a bank
  setting permits lightweight minor revisions.
- **Coverage views** — not-audited-since, high-risk gaps, coverage matrix.

### 4. Checklist Templates — *M2*
- **Templates** — draft/authoring, publish (maker-checker gated), immutable versioning, cloning.
- **Sections & items** — ordered sections/items; per-item response type, risk rating, control link, required flag.
- **Rating scales** — reusable labelled 0–100 point sets for Rating-type items.
- **Response option sets** — organisation-defined conclusion labels/scores per response type (custom vocabulary).

### 5. Audit Engagements — *M4*
- **Lifecycle** — Draft → Planned → In Progress → Under Review → Completed (+ cancel / reopen / return),
  with auto-start and auto-transition rules.
- **Team** — lead/auditee/auditor membership, lead transfer, role changes.
- **Kickoff meeting** — pre-audit meeting scheduling that notifies the auditee in advance.
- **Self-assessment mode** — area owner assesses their own area (lead = auditee, no independent auditor).
- **Budget** — planned-effort hours baseline for budget-vs-actual.

### 6. Execution / Fieldwork — *M5*
- **Responses** — per-item verdict (or custom option) + captured value, comment, **observation**,
  **recommendation**; post-response 0–100 scoring; full before/after response history.
- **Evidence** — upload (magic-byte + hash verified), download, flagging; **evidence requests** to auditees.
- **Item assignment** — assign/bulk-reassign checklist items to team members.
- **Procedures** — sampling, interview, and walkthrough working-paper procedures.
- **Time tracking** — logged effort by category, amendments, budget-vs-actual variance.

### 7. Findings / Exceptions — *M6*
- **Exceptions** — findings with severity (derived from item risk rating), owner, due date, lifecycle.
- **Management Action Plans (MAP)** — auditee remediation submission, approval/rejection, verification.
- **Exception-raising rules** — configurable auto-flag rules per response type (on N/A, on score threshold).
- **Root-cause gaps** — systemic weaknesses across findings, with a **remediation plan** (per-action owners,
  due dates, Open→Completed) and an open→closed lifecycle linking contributing exceptions.

### 8. Controls & Compliance
- **Control register** — controls with status, ownership, and testing history.
- **Control testing** — effectiveness tests, audit-driven (from a checklist item) or ad-hoc.
- **Linkages** — many-to-many control ↔ risk; item ↔ control (findings auto-link to the tested control).
- **Regulations** — regulatory register (authority/category reference data) for compliance mapping.

### 9. Enterprise Risk
- **Risk register** — enterprise/audit risks with categories and scoring.
- **Risk dimensions** — configurable scoring dimensions and scale-label overrides.

### 10. Sanctions & Discipline — *M7*
- **Sanctions cases** — recommend → HR outcome → disciplinary-committee referral → decision → appeal.
- **Sanctions grid** — the offence/sanction matrix reference.
- **Consistency** — cross-case consistency analytics for fair, comparable outcomes.

### 11. Reporting — *M8*
- **Report generation** — audit reports rendered and SHA-256 hash-verified for integrity.
- **Report templates** — configurable report layouts.
- **Report schedules** — recurring generation/distribution.
- **Distribution** — recipient distribution with delivery confirmation; **shared links** for external access.

### 12. Analytics — *M9*
- **KPI dashboards** — bank-wide default dashboards; KPI drilldown.
- **Performance scorecards** — auditor and department/business-unit scorecards (OrgUnit roll-up).
- **Coverage & workload** — coverage heatmap, auditor workload/capacity, plan Gantt.
- **Recurrence detection** — daily scan flagging recurring control weaknesses.
- **Controls compliance** — control-effectiveness roll-up analytics.

### 13. Audit Committee — *M13*
- **AC packs** — generate → approve (CIA) → distribute (with role-gated visibility + downloads).
- **Action items** — committee-raised follow-ups with closure acknowledgement.
- **Workspace & comments** — committee collaboration and threaded comments on pack contents.

### 14. Notifications — *M10*
- **Rules** — event → recipient-resolution → channel routing per domain event.
- **Templates** — per-channel message templates (system + bank overrides).
- **Dispatch & retry** — durable delivery with backoff, dead-lettering, and manual retry.
- **Channels** — Email (SMTP), SMS (gateway), and **Microsoft Teams** (incoming-webhook broadcast).

### 15. Platform & Administration
- **Audit trail** — append-only, DB-trigger-enforced activity log with before/after snapshots. *(M11)*
- **Configuration** — bank settings, maker-checker gates, reference-data lists.
- **Integrations** — outbound webhooks (subscribe by event catalogue) and the AD-over-REST identity gateway.
- **Saved views & search** — reusable filtered views and global search across entities.
- **Engagement lifecycle** — next-best-action engine and a portfolio board across engagements.
- **Branding, theming & i18n** — bank branding, dark/light theme + text-size, English/French localisation.
- **Health & ops** — dependency probes, runtime metrics, background jobs (Hangfire).

---

*Cross-cutting throughout:* Clean Architecture (Domain/Application/Infrastructure/API), hand-rolled CQRS,
optimistic concurrency (rowversion), permission-based authorisation, and the append-only audit trail.
See **[PROJECT_DOCUMENTATION.md](PROJECT_DOCUMENTATION.md)** for architecture, setup, and operations.
