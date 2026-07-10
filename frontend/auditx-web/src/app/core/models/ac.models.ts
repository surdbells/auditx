/**
 * M13 — Audit Committee Workspace models.
 *
 * JSON is camelCase on the wire (camelCase of the backend C# record properties
 * in `AuditX.Application.Ac.Dtos`). Enums (pack status, action-item status) are
 * snake_case strings. DateOnly fields (periodStart/End, dueDate) are ISO
 * `yyyy-MM-dd`; timestamps are ISO 8601 with offset.
 *
 * This is a governance surface: the backend already filters restricted-finding
 * detail per-requester (see {@link AcMaterialFinding.restricted}) and omits all
 * sanctions subject identity (aggregates are by business unit only). The SPA
 * renders only what the API returns.
 *
 * `version` is a base64 rowversion echoed where relevant for optimistic
 * concurrency (pack + action item).
 */

/* =========================================================================
 * AC packs
 * ===================================================================== */

/** Lifecycle status of an AC pack (snake_case wire strings). */
export type AcPackStatus =
  | 'pending'
  | 'generating'
  | 'pending_review'
  | 'approved'
  | 'distributed'
  | 'failed';

/** A produced output artefact descriptor surfaced on the pack metadata. */
export interface AcProducedArtefact {
  format: string;
  contentType: string;
  sizeBytes: number;
  sha256: string;
}

/** AC-pack metadata / status surface (polled while generating). */
export interface AcPack {
  id: string;
  versionNumber: number;
  status: string;
  periodStart: string;
  periodEnd: string;
  acMeetingLabel: string | null;
  sha256Hash: string | null;
  ciaSupplementaryText: string | null;
  requestedFormats: string[];
  producedArtefacts: AcProducedArtefact[];
  failureReason: string | null;
  generatedBy: string;
  requestedAt: string;
  completedAt: string | null;
  approvedBy: string | null;
  approvedAt: string | null;
  version: string;
}

/** Compact list item for the pack list. */
export interface AcPackListItem {
  id: string;
  versionNumber: number;
  status: string;
  periodStart: string;
  periodEnd: string;
  acMeetingLabel: string | null;
  sha256Hash: string | null;
  producedFormats: string[];
  generatedBy: string;
  requestedAt: string;
  completedAt: string | null;
}

/** A single AC-pack distribution-log row. */
export interface AcPackDistribution {
  id: string;
  acPackId: string;
  acPackVersionNumber: number;
  recipientUserId: string;
  dispatchedAt: string;
  dispatchedBy: string;
  outcome: string;
}

/** 202-accepted body returned by AC-pack generation. */
export interface AcPackGenerationResult {
  acPackId: string;
  status: string;
}

/** Result of a distribute call: how many AC recipients were dispatched to. */
export interface AcPackDistributionResult {
  recipientCount: number;
}

/* =========================================================================
 * AC pack analytics (immutable snapshot, restricted-filter applied per requester)
 * ===================================================================== */

/**
 * A material finding as shown to the requester. When {@link restricted} is
 * `true` the detail (title) is nulled by the backend — render a placeholder.
 */
export interface AcMaterialFinding {
  exceptionId: string;
  auditId: string;
  title: string | null;
  severity: string;
  status: string;
  auditableEntityId: string | null;
  raisedAt: string;
  targetDate: string;
  restricted: boolean;
}

/** One exception count for a severity tier. */
export interface AcSeverityCount {
  severity: string;
  count: number;
}

/** Sanctions consistency for one business unit. NO subject identity. */
export interface AcSanctionsConsistencyRow {
  businessUnit: string;
  caseCount: number;
  withinGridCount: number;
  gridAdherencePercent: number;
  deviationCount: number;
  appealCount: number;
  appealRatePercent: number;
}

/** A detected recurrence cluster (aggregate, no subject identity). */
export interface AcRecurrenceCluster {
  id: string;
  auditableEntityId: string;
  category: string | null;
  closedExceptionCount: number;
  windowMonths: number;
  firstOccurredAt: string;
  lastOccurredAt: string;
}

/**
 * The AC pack's analytics surface (from the immutable snapshot). Aggregates are
 * coherent regardless of restriction; detail is hidden per-requester via
 * {@link AcMaterialFinding.restricted}. Sanctions are aggregate-only.
 */
export interface AcPackAnalytics {
  versionNumber: number;
  periodStart: string;
  periodEnd: string;
  totalPlans: number;
  planItemsTotal: number;
  planItemsCompleted: number;
  planCompletionPercent: number;
  openExceptionTotal: number;
  averageClosureDays: number | null;
  exceptionsBySeverity: AcSeverityCount[];
  materialFindings: AcMaterialFinding[];
  sanctionsTotalCases: number;
  sanctionsGridAdherencePercent: number;
  sanctionsAppealRatePercent: number;
  sanctionsByBusinessUnit: AcSanctionsConsistencyRow[];
  recurrenceClusters: AcRecurrenceCluster[];
  generatedAtUtc: string;
}

/* =========================================================================
 * AC dashboard (live aggregates, read-only)
 * ===================================================================== */

/**
 * The read-only AC dashboard. Live aggregates from M9 analytics; sanctions are
 * aggregate-only (no subject id anywhere). Material-finding detail is hidden
 * per-requester for restricted findings; counts stay coherent.
 */
export interface AcDashboard {
  totalPlans: number;
  planItemsTotal: number;
  planItemsCompleted: number;
  planCompletionPercent: number;
  openExceptionTotal: number;
  averageClosureDays: number | null;
  exceptionsBySeverity: AcSeverityCount[];
  materialFindings: AcMaterialFinding[];
  sanctionsTotalCases: number;
  sanctionsGridAdherencePercent: number;
  sanctionsAppealRatePercent: number;
  sanctionsByBusinessUnit: AcSanctionsConsistencyRow[];
  recurrenceClusters: AcRecurrenceCluster[];
}

/* =========================================================================
 * AC action items
 * ===================================================================== */

/** Lifecycle status of an AC action item (snake_case wire strings). */
export type AcActionItemStatus =
  | 'open'
  | 'in_progress'
  | 'closed'
  | 'acknowledged';

export interface AcActionItem {
  id: string;
  title: string;
  description: string | null;
  status: string;
  assignedToUserId: string | null;
  dueDate: string | null;
  closureResponse: string | null;
  createdByUserId: string;
  closedAt: string | null;
  closedByUserId: string | null;
  acknowledgedAt: string | null;
  acknowledgedByUserId: string | null;
  version: string;
}

/* =========================================================================
 * AC comments
 * ===================================================================== */

/** The polymorphic target a comment is attached to. */
export type AcCommentTargetType = 'plan' | 'pack' | 'finding';

export interface AcComment {
  id: string;
  targetType: string;
  targetId: string;
  commentText: string;
  authorUserId: string;
  commentedAt: string;
}

/* =========================================================================
 * Finding visibility restriction
 * ===================================================================== */

export interface FindingVisibilityRestriction {
  id: string;
  findingType: string;
  findingId: string;
  allowedUserIds: string[];
  reason: string | null;
  restrictedByUserId: string;
  restrictedAt: string;
}

/* =========================================================================
 * Request payloads
 * ===================================================================== */

/** Body for POST /ac-packs/generate. HTML is always produced; `docx` adds DOCX. */
export interface GenerateAcPackRequest {
  periodStart: string;
  periodEnd: string;
  acMeetingLabel?: string | null;
  docx?: boolean;
}

/** Query for GET /ac-packs. */
export interface AcPackQuery {
  status?: string | null;
  page?: number;
  pageSize?: number;
}

/** Body for PATCH /ac-packs/{id}/cia-text. */
export interface UpdateAcPackCiaTextRequest {
  supplementaryText: string | null;
}

/** Body for POST /ac-packs/{id}/approve. Optional last-mile text edit. */
export interface ApproveAcPackRequest {
  supplementaryText?: string | null;
}

/** Body for POST /ac-action-items. */
export interface CreateAcActionItemRequest {
  title: string;
  description?: string | null;
  assignedToUserId?: string | null;
  dueDate?: string | null;
}

/**
 * Body for PATCH /ac-action-items/{id}. A non-blank `closureResponse` closes the
 * item (blank → 422); otherwise `markInProgress` moves it open → in_progress.
 */
export interface UpdateAcActionItemRequest {
  markInProgress?: boolean;
  closureResponse?: string | null;
}

/** Body for POST /ac-comments. */
export interface AddAcCommentRequest {
  targetType: AcCommentTargetType;
  targetId: string;
  comment: string;
}

/** Body for POST /findings/{type}/{id}/restrict-visibility. */
export interface RestrictFindingVisibilityRequest {
  allowedUserIds?: string[];
  reason?: string | null;
}