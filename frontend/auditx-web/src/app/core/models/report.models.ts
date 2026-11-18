/**
 * M8 — Reports models.
 *
 * Reports are generated asynchronously: the POST returns a 202 with the new
 * report id and a `pending` status; callers then poll GET /reports/{id} until
 * the status settles on `completed` or `failed`. JSON is camelCase on the wire.
 *
 * `version` is a base64 rowversion echoed where relevant for optimistic
 * concurrency (e.g. report templates).
 */

/** Lifecycle status of a report generation. `expired` = past retention; artefacts no longer served. */
export type ReportStatus = 'pending' | 'running' | 'completed' | 'failed' | 'expired';

/**
 * The kind of report. `audit_engagement` is the per-audit engagement report;
 * the rest are standalone (cross-audit) analytics reports with no single audit.
 */
export type ReportKind =
  | 'audit_engagement'
  | 'executive_summary'
  | 'annual_plan_status'
  | 'kpi_pack'
  | 'audit_coverage'
  | 'findings_register'
  | 'sanctions_consistency'
  | 'performance_scorecards';

/** The standalone report kinds (everything except the per-audit engagement report). */
export type StandaloneReportKind = Exclude<ReportKind, 'audit_engagement'>;

/** A produced output artefact for a completed report version. */
export interface ReportArtefact {
  format: string;
  contentType: string;
  sizeBytes: number;
  sha256: string;
}

/** Full report aggregate (metadata / status surface — polled while running). */
/**
 * The branding a report was issued under, frozen when it was generated: the institution name, its colours and the
 * SHA-256 of the logo embedded in the artefacts (null when it was issued without a logo). A later rebrand never
 * changes it.
 */
export interface ReportBranding {
  institutionName: string;
  primaryColor: string;
  accentColor: string;
  logoSha256: string | null;
}

/** A draft is for discussion with management; a final is signed off by the Chief Internal Auditor before it goes out. */
export type ReportStage = 'draft' | 'final';

/** Where a final report stands in its sign-off (drafts and older reports need none). */
export type ReportSignOffStatus = 'not_required' | 'awaiting' | 'signed_off' | 'returned';

export interface Report {
  id: string;
  /** Null for standalone (cross-audit) reports. */
  auditId: string | null;
  kind: ReportKind;
  versionNumber: number;
  status: ReportStatus;
  sha256Hash: string | null;
  templateId: string;
  templateVersionSnapshot: number;
  requestedFormats: string[];
  producedArtefacts: ReportArtefact[];
  failureReason: string | null;
  generatedBy: string;
  requestedAt: string;
  completedAt: string | null;
  version: string;
  /** Null until generation starts, and for reports issued before branding was recorded. */
  branding?: ReportBranding | null;
  stage: ReportStage;
  signOffStatus: ReportSignOffStatus;
  /** Who signed the final off, or returned it for changes. */
  signOffBy?: string | null;
  signOffAt?: string | null;
  /** The signer's remark, or the reason it was returned. */
  signOffNote?: string | null;
  /** True once the report has been sent to anyone. */
  isDistributed: boolean;
  /** True when it may be distributed now: a completed draft, or a signed-off final. */
  isDistributable: boolean;
}

/** A comment on a draft report. */
export interface ReportComment {
  id: string;
  reportId: string;
  reportVersionNumber: number;
  authorUserId: string;
  body: string;
  postedAt: string;
}

/** A final report awaiting the Chief Internal Auditor's sign-off. */
export interface ReportSignOffQueueItem {
  reportId: string;
  auditId: string;
  auditName: string;
  versionNumber: number;
  generatedBy: string;
  completedAt: string | null;
}

/** Lightweight row for the per-audit / per-kind report version list. */
export interface ReportListItem {
  id: string;
  /** Null for standalone (cross-audit) reports. */
  auditId: string | null;
  kind: ReportKind;
  versionNumber: number;
  status: ReportStatus;
  sha256Hash: string | null;
  producedFormats: string[];
  generatedBy: string;
  requestedAt: string;
  completedAt: string | null;
  stage: ReportStage;
  signOffStatus: ReportSignOffStatus;
}

/** A single distribution record for a report version. */
export interface ReportDistribution {
  id: string;
  reportId: string;
  reportVersionNumber: number;
  recipientUserId: string | null;
  recipientEmail: string | null;
  dispatchedAt: string;
  dispatchedBy: string;
  outcome: string;
  /** When/who confirmed the outcome (null while pending). */
  outcomeRecordedAt: string | null;
  outcomeRecordedBy: string | null;
}

/** Stored-vs-recomputed integrity check on a report artefact. */
export interface ReportHashVerification {
  storedHash: string | null;
  recomputedHash: string;
  match: boolean;
}

/** A report template version. */
export interface ReportTemplate {
  id: string;
  name: string;
  versionNumber: number;
  templateDefinitionJson: string;
  isActive: boolean;
  activationReason: string | null;
  createdByUserId: string;
  createdAtUtc: string;
  activatedBy: string | null;
  activatedAt: string | null;
  version: string;
}

/* ---- Request payloads ---- */

/** Body for POST /audits/{auditId}/reports. `docx` opts a DOCX artefact in. */
export interface GenerateReportRequest {
  docx?: boolean;
  /** Draft (the default) or final. */
  stage?: ReportStage;
}

/** Body for POST /reports/standalone. `kind` is the standalone report kind. */
export interface GenerateStandaloneReportRequest {
  kind: StandaloneReportKind;
  docx?: boolean;
}

/** 202-accepted body returned by report generation. */
export interface GenerateReportResult {
  reportId: string;
  status: ReportStatus;
}

/** Body for POST /reports/{id}/distribute. */
export interface DistributeReportRequest {
  recipientUserIds: string[];
  recipientEmailAddresses: string[];
  /** Role names; each is expanded server-side to its active members. */
  recipientRoleNames?: string[];
}

/** Result of a distribute call. */
export interface DistributeReportResult {
  recipientCount: number;
}

/** Body for POST /report-templates. */
export interface CreateReportTemplateRequest {
  name: string;
  templateDefinitionJson: string;
}

/** Body for PATCH /report-templates/{id}/activate (reason >= 20 chars). */
export interface ActivateReportTemplateRequest {
  reason: string;
}
