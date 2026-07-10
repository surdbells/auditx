/**
 * M6 — Exceptions & Management Action Plans (MAP) models.
 *
 * The API emits and expects snake_case enum values. `version` is a base64
 * rowversion echoed on every mutation for optimistic concurrency: after each
 * successful mutation the response carries a fresh `version` that the next
 * mutation must send.
 *
 * Evidence reuses the M5 `EvidenceFile` aggregate (no version on evidence ops).
 */

/** Severity of a raised exception. */
export type ExceptionSeverity = 'low' | 'medium' | 'high' | 'critical';

/**
 * Lifecycle status of an exception. The API may additionally return the derived
 * display value `pending_cia_approval` when a Critical exception sits in
 * pending_closure awaiting CIA countersign — treat it as a display-only status.
 */
export type ExceptionStatus =
  | 'open'
  | 'map_submitted'
  | 'map_approved'
  | 'map_rejected'
  | 'pending_closure'
  | 'closed'
  | 'cancelled'
  | 'pending_cia_approval';

/** Status of a single management-action-plan action. */
export type MapActionStatus = 'pending' | 'complete';

/** A single management-action-plan action. */
export interface MapAction {
  id: string;
  exceptionId: string;
  description: string;
  ownerUserId: string;
  targetDate: string;
  expectedEvidenceType?: string | null;
  status: MapActionStatus;
  completedAt?: string | null;
  completedBy?: string | null;
}

/** Full exception aggregate. */
export interface Exception {
  id: string;
  auditId: string;
  checklistItemId: string;
  auditableEntityId?: string | null;
  title: string;
  severity: ExceptionSeverity;
  status: ExceptionStatus;
  rootCause?: string | null;
  recommendation?: string | null;
  category?: string | null;
  /** Structured root-cause taxonomy code (P2-A). */
  rootCauseCategory?: string | null;
  ownerUserId: string;
  raisedByUserId: string;
  raisedAt: string;
  targetDate: string;
  /** Quantified financial exposure (null when not assessed). */
  financialImpact?: number | null;
  financialImpactCurrency?: string | null;
  targetDateOverridden: boolean;
  isRecurrence: boolean;
  recurrenceOfExceptionId?: string | null;
  ciaPending: boolean;
  isOverdue: boolean;
  daysPastTarget: number;
  mapSubmittedAt?: string | null;
  mapApprovedAt?: string | null;
  mapRejectionReason?: string | null;
  closureEvidenceNote?: string | null;
  closedBy?: string | null;
  closedAt?: string | null;
  ciaCountersignedBy?: string | null;
  cancellationReason?: string | null;
  version: string;
  mapActions: MapAction[];
}

/** Lightweight row for the exceptions tracker list. */
export interface ExceptionListItem {
  id: string;
  auditId: string;
  title: string;
  severity: ExceptionSeverity;
  status: ExceptionStatus;
  ownerUserId: string;
  targetDate: string;
  isOverdue: boolean;
  daysPastTarget: number;
  isRecurrence: boolean;
  /** ISO datetime the exception was raised. */
  raisedAt: string;
}

/** A single entry in an exception's history timeline. */
export interface ExceptionHistoryEntry {
  id: string;
  eventType: string;
  actorUserId?: string | null;
  occurredAtUtc: string;
  payloadJson?: string | null;
}

/* ---- Request payloads ---- */

export interface RaiseExceptionRequest {
  checklistItemId: string;
  title: string;
  severity: ExceptionSeverity;
  rootCause: string;
  recommendation: string;
  category?: string | null;
  rootCauseCategory?: string | null;
  ownerUserId: string;
  targetDateOverride?: string | null;
  overrideRationale?: string | null;
}

export interface MapActionInput {
  description: string;
  ownerUserId: string;
  targetDate: string;
  expectedEvidenceType?: string | null;
}

export interface SubmitMapRequest {
  actions: MapActionInput[];
  version: string;
}

export interface ChangeSeverityRequest {
  severity: ExceptionSeverity;
  reason: string;
  version: string;
}

export interface ReassignOwnerRequest {
  ownerUserId: string;
  version: string;
}

export interface VersionRequest {
  version: string;
}

export interface ReasonVersionRequest {
  reason: string;
  version: string;
}

export interface CloseExceptionRequest {
  closureNote?: string | null;
  version: string;
}

/**
 * Result of POST /map/approve: either the updated Exception (applied directly),
 * or, when maker-checker-gated, a 202 carrying a pendingActionId.
 */
export interface ApproveMapResult {
  exception?: Exception;
  pendingActionId?: string;
}

/** List query parameters for the cross-audit exceptions tracker. */
export interface ExceptionQuery {
  status?: ExceptionStatus | '';
  severity?: ExceptionSeverity | '';
  owner?: string;
  entity?: string;
  audit?: string;
  /** Annual-plan id: matches exceptions whose audit rolls up to this plan. */
  plan?: string;
  category?: string;
  /** Free-text search over title / root cause. */
  search?: string;
  recurrence?: boolean;
  overdue?: boolean;
  /** ISO datetime lower bound on raised-at (inclusive). */
  raisedFrom?: string;
  /** ISO datetime upper bound on raised-at (inclusive). */
  raisedTo?: string;
  cursor?: string | null;
  limit?: number;
}
