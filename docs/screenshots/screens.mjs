// Per-screen annotation metadata, keyed by the exact screen label used in capture.mjs.
// purpose = 1-2 sentence description shown in the legend + PDF caption.
// spot    = the screen-specific callout (pin 5), pointing at the main workspace region.
// cat     = section grouping for the contents page.

export const META = {
  // ---- Getting started ----
  'Login': {
    cat: 'Getting started',
    purpose: 'The single sign-in page. Staff authenticate with their bank username and password; sessions are permission-scoped, so each user only sees the modules their role allows.',
    spot: 'Credential form — username, password, and sign-in.',
  },
  'Dashboard': {
    cat: 'Getting started',
    purpose: "The landing workspace after sign-in. It summarises the work that matters to the signed-in user — open audits, findings awaiting action, evidence requests and upcoming deadlines — with quick links into each.",
    spot: 'KPI tiles and work queues tailored to the signed-in user.',
  },
  'My Evidence Requests (auditee upload worklist)': {
    cat: 'Getting started',
    purpose: 'The auditee worklist. When an auditor requests a document, it lands here; the auditee uploads the requested files straight against each request, and the status flips to Received on the auditor’s side.',
    spot: 'Outstanding document requests, each with an upload action.',
  },

  // ---- Audit universe & planning ----
  'Audit Universe — Entities': {
    cat: 'Audit universe & planning',
    purpose: 'The catalogue of everything that can be audited — branches, processes, systems, products. Each entity carries an inherent-risk score that drives how often it is picked up in the annual plan.',
    spot: 'Auditable entities with their risk scores and last-audited dates.',
  },
  'Risk Scoring Dimensions': {
    cat: 'Audit universe & planning',
    purpose: 'The weighted factors (e.g. financial exposure, regulatory impact, change velocity) that combine into each entity’s risk score. Editing the weights here re-prioritises the whole universe.',
    spot: 'Risk dimensions and their scoring weights.',
  },
  'Organisational Units': {
    cat: 'Audit universe & planning',
    purpose: 'The bank’s org structure — divisions, departments and branches — used to scope audits, route findings to the right owner, and roll analytics up by unit.',
    spot: 'The organisation hierarchy tree.',
  },
  'Annual Plans': {
    cat: 'Audit universe & planning',
    purpose: 'The yearly audit programme. Plans list the engagements scheduled for the year, their status, and how they map back to risk coverage; approval is maker-checker controlled.',
    spot: 'Planned engagements for the selected year.',
  },
  'Annual Plan — detail': {
    cat: 'Audit universe & planning',
    purpose: 'One plan opened up — the engagements it contains, their timing on a Gantt-style timeline, resourcing, and the risk it covers. Where the plan is built and submitted for approval.',
    spot: 'Plan line items, timeline and coverage.',
  },
  'Coverage Analytics': {
    cat: 'Audit universe & planning',
    purpose: 'Shows how much of the audit universe the plan actually covers — which high-risk entities are scheduled and which are being left unaudited — so gaps are visible before the year starts.',
    spot: 'Coverage heatmap of risk vs. planned assurance.',
  },

  // ---- Templates & response config ----
  'Checklist Templates': {
    cat: 'Templates & response config',
    purpose: 'Reusable checklists auditors run during fieldwork. Templates standardise the questions asked for a given audit type so every engagement is executed consistently.',
    spot: 'Library of checklist templates.',
  },
  'Template Editor': {
    cat: 'Templates & response config',
    purpose: 'Build or edit a checklist — add sections and items, mark items required (default) or optional, and set the response type each item expects. Versioned so in-flight audits keep their original wording.',
    spot: 'Section/item builder with required toggles.',
  },
  'Rating Scales': {
    cat: 'Templates & response config',
    purpose: 'The rating scales used across audits (e.g. risk ratings, maturity levels). Defined once and reused so ratings mean the same thing everywhere.',
    spot: 'Scales and their ordered rating levels.',
  },
  'Response Labels (custom option sets)': {
    cat: 'Templates & response config',
    purpose: 'Lets the organisation name its own answer options per response type — the words behind Pass / Fail / Partial and any other set — so checklists speak the bank’s language.',
    spot: 'Custom option sets per response type.',
  },

  // ---- Audits & execution ----
  'Audits': {
    cat: 'Audits & execution',
    purpose: 'The register of all audit engagements with their status, lead auditor, scope and dates. The entry point into executing, reviewing and reporting on each audit.',
    spot: 'All engagements with status and lead.',
  },
  'Audit — detail': {
    cat: 'Audits & execution',
    purpose: 'A single engagement’s home — scope, team, timeline, linked findings and evidence, and the actions to move it through its lifecycle.',
    spot: 'Engagement overview with linked findings and evidence.',
  },
  'Audit Execution / Fieldwork': {
    cat: 'Audits & execution',
    purpose: 'Where auditors do fieldwork — work through the checklist, record responses with observations and recommendations, attach evidence, and raise exceptions as issues are found.',
    spot: 'Checklist workspace with per-item responses.',
  },
  'Audit Reports': {
    cat: 'Audits & execution',
    purpose: 'The reports produced for an engagement — draft, review and issue the audit report, with findings and management responses pulled through automatically.',
    spot: 'Report drafts and issued versions for this audit.',
  },
  'Engagement Lifecycle — portfolio board': {
    cat: 'Audits & execution',
    purpose: 'A portfolio board of every live engagement grouped by lifecycle stage (planning → fieldwork → reporting → closed), giving audit management a single view of where everything sits.',
    spot: 'Engagements as cards across lifecycle columns.',
  },
  'Engagement Journey / next-best-action': {
    cat: 'Audits & execution',
    purpose: 'One engagement’s journey through its stages, surfacing the next best action and any blockers so the lead auditor always knows what to do next.',
    spot: 'Stage timeline and recommended next action.',
  },

  // ---- Findings, controls & risk ----
  'Findings / Exceptions register': {
    cat: 'Findings, controls & risk',
    purpose: 'The central register of exceptions (findings) raised across all audits — severity, owner, due date and remediation status — filterable and the source of most reporting.',
    spot: 'All findings with severity and status.',
  },
  'Finding — detail': {
    cat: 'Findings, controls & risk',
    purpose: 'One finding in full — the issue, root cause, agreed management action plan (MAP), owner and evidence. Auditees submit their MAP and upload remediation evidence here; it can also be flagged as a recurrence.',
    spot: 'Finding narrative, MAP, owner and remediation evidence.',
  },
  'Root-cause Gaps': {
    cat: 'Findings, controls & risk',
    purpose: 'Groups findings by their underlying root-cause gap so systemic weaknesses — not just individual issues — become visible and can be remediated at source.',
    spot: 'Findings clustered by root-cause theme.',
  },
  'Control Register': {
    cat: 'Findings, controls & risk',
    purpose: 'The library of controls the bank relies on, their owners and how they map to risks and regulations — the backbone for control-testing and compliance reporting.',
    spot: 'Controls with owners and linked risks.',
  },
  'Regulations Register': {
    cat: 'Findings, controls & risk',
    purpose: 'The regulatory obligations the bank is subject to, mapped to the controls that satisfy them, so compliance coverage can be evidenced.',
    spot: 'Regulations mapped to controls.',
  },
  'Enterprise Risk Register': {
    cat: 'Findings, controls & risk',
    purpose: 'The enterprise risks the audit function tracks — likelihood, impact and treatment — linked to the controls and audits that provide assurance over them.',
    spot: 'Risks with likelihood/impact and linked assurance.',
  },
  'Exception-raising Rules': {
    cat: 'Findings, controls & risk',
    purpose: 'Rules that auto-raise exceptions when a checklist response meets defined conditions, so material issues are captured consistently rather than relying on memory.',
    spot: 'Conditional rules that generate findings.',
  },

  // ---- Sanctions ----
  'Sanctions Cases': {
    cat: 'Sanctions',
    purpose: 'Disciplinary/sanctions cases arising from audit findings — who, what and the current stage — managed through to a decision.',
    spot: 'Sanctions cases with stage and subject.',
  },
  'Sanctions Case — detail': {
    cat: 'Sanctions',
    purpose: 'A single sanctions case — the linked finding, the people involved, the timeline of actions, and the disciplinary decision with its audit trail.',
    spot: 'Case timeline, parties and decision.',
  },
  'Disciplinary Committee Queue': {
    cat: 'Sanctions',
    purpose: 'The queue of cases awaiting the disciplinary committee, prioritised so nothing sits unreviewed past its SLA.',
    spot: 'Cases pending committee review.',
  },
  'Sanctions Grid': {
    cat: 'Sanctions',
    purpose: 'The sanctions matrix — the reference grid mapping offence types and severities to the applicable disciplinary outcome, keeping decisions consistent and defensible.',
    spot: 'Offence-vs-outcome reference grid.',
  },

  // ---- Reporting ----
  'Reports': {
    cat: 'Reporting',
    purpose: 'The reporting hub — generated audit and thematic reports across engagements, ready to open, export or circulate.',
    spot: 'Generated reports across engagements.',
  },
  'Report — detail': {
    cat: 'Reporting',
    purpose: 'One report rendered in full, with its findings, ratings and management responses, ready for review and issue.',
    spot: 'Rendered report content and sections.',
  },
  'Report Templates': {
    cat: 'Reporting',
    purpose: 'The templates that define report structure and branding, so every report the bank issues has a consistent, professional format.',
    spot: 'Report layout templates.',
  },
  'Report Schedules': {
    cat: 'Reporting',
    purpose: 'Scheduled, recurring report generation and delivery — set a report to run and be distributed automatically on a cadence.',
    spot: 'Recurring report schedules and recipients.',
  },

  // ---- Analytics ----
  'Analytics — KPIs': {
    cat: 'Analytics',
    purpose: 'The analytics home — headline KPIs for the audit function (cycle time, on-time closure, coverage, open findings) each drillable into the detail behind the number.',
    spot: 'Headline KPI cards for the function.',
  },
  'Custom Dashboard': {
    cat: 'Analytics',
    purpose: 'A user-built dashboard — assemble the charts and metrics that matter to a given audience from the available analytics tiles.',
    spot: 'User-assembled analytics tiles.',
  },
  'Performance Scorecards': {
    cat: 'Analytics',
    purpose: 'Scorecards rating auditor and team performance across delivery, quality and timeliness measures.',
    spot: 'Per-auditor / team performance scores.',
  },
  'Auditor Throughput': {
    cat: 'Analytics',
    purpose: 'How much work each auditor is completing over time — engagements and findings closed — to spot capacity and bottlenecks.',
    spot: 'Throughput trend by auditor.',
  },
  'Org-unit Scorecards': {
    cat: 'Analytics',
    purpose: 'Assurance and findings rolled up by organisational unit, showing which parts of the bank carry the most open risk.',
    spot: 'Findings and assurance by org unit.',
  },
  'Budget vs Actual': {
    cat: 'Analytics',
    purpose: 'Planned audit hours/cost against actuals per engagement, so overruns and under-utilisation surface early.',
    spot: 'Budgeted vs. actual effort per engagement.',
  },
  'Auditor Workload': {
    cat: 'Analytics',
    purpose: 'Current assignment load per auditor — who is over- or under-allocated right now — to support resourcing decisions.',
    spot: 'Live workload per auditor.',
  },
  'KPI Trends': {
    cat: 'Analytics',
    purpose: 'The key metrics tracked over time so improvement or drift in the audit function is visible, not just this month’s snapshot.',
    spot: 'KPI time-series with trend lines.',
  },
  'Risk Heatmap': {
    cat: 'Analytics',
    purpose: 'A likelihood-vs-impact heatmap of the risk landscape, concentrating attention on the top-right (high/high) cells.',
    spot: 'Likelihood × impact risk grid.',
  },
  'Controls Compliance': {
    cat: 'Analytics',
    purpose: 'How controls are performing across the estate — pass/fail rates from testing — to evidence the control environment’s health.',
    spot: 'Control pass/fail rates.',
  },
  'Recurrence Clusters': {
    cat: 'Analytics',
    purpose: 'Detects findings that keep recurring and groups them into clusters, so repeat issues get systemic treatment rather than being closed over and over.',
    spot: 'Clusters of recurring findings.',
  },
  'Recurrence Cluster — detail': {
    cat: 'Analytics',
    purpose: 'One recurrence cluster expanded — the member findings, where and when they recur, and the shared root cause to address.',
    spot: 'Member findings and shared root cause.',
  },

  // ---- Audit committee ----
  'Audit Committee — workspace': {
    cat: 'Audit committee',
    purpose: 'The board-level workspace — curated packs, summaries and metrics prepared for the audit committee, giving non-executives assurance without operational noise.',
    spot: 'Committee packs and summary metrics.',
  },

  // ---- Identity & access ----
  'Users': {
    cat: 'Identity & access',
    purpose: 'User administration — accounts, their status and assigned roles. Where access is granted and revoked.',
    spot: 'User accounts with roles and status.',
  },
  'User — detail': {
    cat: 'Identity & access',
    purpose: 'A single user — profile, assigned roles and the effective permissions those roles grant, plus account status controls.',
    spot: 'Profile, roles and effective permissions.',
  },
  'Roles': {
    cat: 'Identity & access',
    purpose: 'The roles that bundle permissions (e.g. Lead Auditor, Auditee, AC Member). Assigning a role is how access is granted at scale.',
    spot: 'Roles and how many users hold each.',
  },
  'Role Editor': {
    cat: 'Identity & access',
    purpose: 'Compose a role by ticking the exact permissions it grants; changes cascade to everyone holding the role.',
    spot: 'Permission matrix for the selected role.',
  },
  'Maker-checker Queue': {
    cat: 'Identity & access',
    purpose: 'Sensitive changes are made by one person and approved by another. This queue holds pending changes awaiting a second-person check before they take effect.',
    spot: 'Pending changes awaiting approval.',
  },

  // ---- Platform administration ----
  'Bank Settings / Administration': {
    cat: 'Platform administration',
    purpose: 'Organisation-level settings — the bank’s identity, defaults and platform-wide options that shape how AuditX behaves for everyone.',
    spot: 'Organisation settings and defaults.',
  },
  'Audit Trail': {
    cat: 'Platform administration',
    purpose: 'The append-only system audit trail — every meaningful change, who made it and when. Read-only and tamper-evident, for forensic and compliance review.',
    spot: 'Immutable log of system actions.',
  },
  'Configuration Store': {
    cat: 'Platform administration',
    purpose: 'The catalogue of configurable settings grouped by domain, each editable through maker-checker so platform behaviour changes are controlled and traceable.',
    spot: 'Configuration domains.',
  },
  'Configuration — exception defaults': {
    cat: 'Platform administration',
    purpose: 'One configuration domain opened — the default values (here, for exceptions) that new records inherit, edited in a controlled form.',
    spot: 'Editable defaults for this domain.',
  },
  'Reference Data': {
    cat: 'Platform administration',
    purpose: 'The managed lists behind dropdowns across the app (categories, types, statuses), curated in one place so terminology stays consistent.',
    spot: 'Managed reference lists.',
  },
  'Integrations': {
    cat: 'Platform administration',
    purpose: 'Connections to outside systems — email, Teams, SMS and others — configured and health-checked here so notifications and hand-offs actually land.',
    spot: 'Configured external integrations.',
  },
  'Webhooks': {
    cat: 'Platform administration',
    purpose: 'Outbound webhooks that push AuditX events to other systems in real time, with delivery status so failures are visible.',
    spot: 'Webhook endpoints and delivery status.',
  },
  'Notifications (rules / templates / dispatches)': {
    cat: 'Platform administration',
    purpose: 'The notification engine — rules (when to notify), templates (what to say), and the dispatch log (what was sent). Drives the emails/SMS/Teams messages the platform sends.',
    spot: 'Rules, templates and the dispatch log.',
  },
  'Evidence Integrity (flagged files)': {
    cat: 'Platform administration',
    purpose: 'Watches uploaded evidence for integrity problems — checksum mismatches or tampering — and flags anything suspect for review, protecting the evidentiary chain.',
    spot: 'Files flagged for integrity review.',
  },
  'My Notification Preferences': {
    cat: 'Platform administration',
    purpose: 'Each user’s own control over which notifications they receive and on which channel — a per-person settings page.',
    spot: 'Personal notification channel choices.',
  },
};
