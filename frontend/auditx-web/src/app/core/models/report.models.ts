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

/** Lifecycle status of a report generation. */
export type ReportStatus = 'pending' | 'running' | 'completed' | 'failed';

/** A produced output artefact for a completed report version. */
export interface ReportArtefact {
  format: string;
  contentType: string;
  sizeBytes: number;
  sha256: string;
}

/** Full report aggregate (metadata / status surface — polled while running). */
export interface Report {
  id: string;
  auditId: string;
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
}

/** Lightweight row for the per-audit report version list. */
export interface ReportListItem {
  id: string;
  auditId: string;
  versionNumber: number;
  status: ReportStatus;
  sha256Hash: string | null;
  producedFormats: string[];
  generatedBy: string;
  requestedAt: string;
  completedAt: string | null;
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
