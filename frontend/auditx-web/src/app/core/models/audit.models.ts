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
