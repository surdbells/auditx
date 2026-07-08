/**
 * M4 — Audit Lifecycle models.
 *
 * The API emits and expects snake_case enum values. `version` is a base64
 * rowversion echoed on every mutation for optimistic concurrency: after each
 * successful mutation the response carries a fresh `version` that the next
 * mutation must send.
 *
 * Date fields (startDate, targetEndDate, actualEndDate) are ISO `yyyy-MM-dd`.
 */

import { ResponseType } from './template.models';

/** Lifecycle state of an audit. */
export type AuditStatus =
  | 'draft'
  | 'planned'
  | 'in_progress'
  | 'under_review'
  | 'completed'
  | 'cancelled';

/** Role a member plays on an audit team. */
export type TeamRole = 'lead' | 'auditor' | 'reviewer' | 'auditee';

/** Progress state of a single checklist item. */
export type ChecklistItemState = 'not_started' | 'in_progress' | 'responded';

/** Valid targets for the generic transition endpoint. */
export type TransitionTarget =
  | 'planned'
  | 'in_progress'
  | 'under_review'
  | 'completed'
  | 'draft';

/** A member of the audit team. */
export interface AuditTeamMember {
  id: string;
  userId: string;
  teamRole: TeamRole;
  isActive: boolean;
  addedAt: string;
  removedAt: string | null;
}

/** A named, ordered grouping of checklist items within an audit (first-class, CRUD-managed). */
export interface AuditSection {
  id: string;
  name: string;
  orderIndex: number;
}

/** A single checklist item attached to an audit. */
export interface AuditChecklistItem {
  id: string;
  sectionName: string | null;
  orderIndex: number;
  prompt: string;
  referenceNotes: string | null;
  responseType: ResponseType;
  assignedUserId: string | null;
  isRequired: boolean;
  itemState: ChecklistItemState;
}

/** Full aggregate for a single audit. */
export interface Audit {
  id: string;
  name: string;
  scopeDescription: string | null;
  auditType: string;
  status: AuditStatus;
  startDate: string;
  targetEndDate: string | null;
  actualEndDate: string | null;
  templateId: string | null;
  templateVersion: number | null;
  planItemId: string | null;
  leadUserId: string;
  auditeeUserId: string;
  cancellationReason: string | null;
  version: string;
  teamMembers: AuditTeamMember[];
  sections: AuditSection[];
  checklistItems: AuditChecklistItem[];
}

/** Lightweight row for the audits list. */
export interface AuditListItem {
  id: string;
  name: string;
  auditType: string;
  status: AuditStatus;
  startDate: string;
  targetEndDate: string | null;
  leadUserId: string;
  checklistItemCount: number;
  respondedItemCount: number;
}

/** Aggregate counts by status. */
export interface AuditCounts {
  byStatus: Record<string, number>;
}

/* ---- Request payloads ---- */

export interface CreateAuditRequest {
  name: string;
  auditType: string;
  startDate: string;
  targetEndDate?: string | null;
  scopeDescription?: string | null;
  templateId?: string | null;
  planItemId?: string | null;
  leadUserId: string;
  auditeeUserId: string;
  teamMemberUserIds?: string[];
  backdatingOverride: boolean;
  backdatingReason?: string | null;
}

export interface UpdateAuditRequest {
  name: string;
  scopeDescription?: string | null;
  startDate: string;
  targetEndDate: string | null;
  version: string;
}

export interface TransitionAuditRequest {
  targetState: TransitionTarget;
  reason?: string | null;
  version: string;
}

export interface CancelAuditRequest {
  reason: string;
  version: string;
}

export interface AddTeamMemberRequest {
  userId: string;
  teamRole: TeamRole;
  version: string;
}

export interface TransferLeadRequest {
  newLeadUserId: string;
  removeOutgoing: boolean;
  version: string;
}

export interface AddChecklistItemRequest {
  prompt: string;
  referenceNotes?: string | null;
  responseType: ResponseType;
  sectionName?: string | null;
  isRequired: boolean;
  assignedUserId?: string | null;
  version: string;
}

export interface UpdateChecklistItemRequest {
  prompt: string;
  referenceNotes?: string | null;
  sectionName?: string | null;
  isRequired: boolean;
  assignedUserId?: string | null;
  version: string;
}

/** List query parameters for the audits list. */
export interface AuditQuery {
  status?: AuditStatus | '';
  auditType?: string;
  lead?: string;
  planItem?: string;
  cursor?: string | null;
  limit?: number;
}

/* =========================================================================
 * M5 — Audit Execution / Fieldwork
 *
 * Response submit/discard, item assignment, bulk-reassign and fail-judgement
 * reuse the audit `version` for optimistic concurrency. Evidence is a separate
 * aggregate and carries NO audit version.
 * ========================================================================= */

/** A verdict recorded against a checklist item. `null` = draft (no verdict yet). */
export type ResponseVerdict = 'pass' | 'fail' | 'na';

/** A single recorded (or draft) response to a checklist item. */
export interface ChecklistResponse {
  id: string;
  auditId: string;
  checklistItemId: string;
  verdict: ResponseVerdict | null;
  comment?: string | null;
  responderUserId: string;
  isDraft: boolean;
  responseVersion: number;
  respondedAt?: string | null;
}

/** An evidence file attached to a response. */
export interface EvidenceFile {
  id: string;
  auditId: string;
  contextType: string;
  contextId: string;
  originalFilename: string;
  mimeType: string;
  sizeBytes: number;
  sha256Hash: string;
  uploadedBy: string;
  uploadedAt: string;
  isFlagged: boolean;
}

/** Per-item progress row in the checklist-progress aggregate. */
export interface ChecklistProgressItem {
  itemId: string;
  sectionName?: string | null;
  orderIndex: number;
  prompt: string;
  itemState: ChecklistItemState;
  verdict?: ResponseVerdict | null;
  isRequired: boolean;
  assignedUserId?: string | null;
  hasException: boolean;
}

/** Aggregate progress across an audit's checklist. */
export interface ChecklistProgress {
  totalItems: number;
  respondedItems: number;
  inProgressItems: number;
  notStartedItems: number;
  items: ChecklistProgressItem[];
}

/** A single entry in a response's audit history timeline. */
export interface ResponseHistoryEntry {
  id: string;
  eventType: string;
  actorUserId?: string | null;
  occurredAtUtc: string;
  stateJson?: string | null;
}

/** Review-time summary counts across the checklist. */
export interface ReviewSummary {
  totalItems: number;
  responded: number;
  pass: number;
  fail: number;
  na: number;
  exceptions: number;
}

/** A failed item that lacks a recorded exception, surfaced for reviewer judgement. */
export interface FailWithoutExceptionItem {
  itemId: string;
  prompt: string;
  comment?: string | null;
  assignedUserId?: string | null;
}

/** Result of the fail-without-exception review check. */
export interface FailWithoutExceptionResult {
  count: number;
  items: FailWithoutExceptionItem[];
}

/* ---- Request payloads ---- */

export interface SubmitResponseRequest {
  verdict?: ResponseVerdict | null;
  comment?: string | null;
  isDraft: boolean;
  version: string;
}

export interface AssignItemRequest {
  assignedUserId?: string | null;
  version: string;
}

export interface BulkReassignEntry {
  itemId: string;
  assigneeUserId?: string | null;
}

export interface BulkReassignRequest {
  assignments: BulkReassignEntry[];
  version: string;
}

export interface FailJudgementRequest {
  justification: string;
  version: string;
}
