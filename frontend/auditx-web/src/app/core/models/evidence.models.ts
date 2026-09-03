/**
 * P2-D — expected / requested evidence: the "requested" side (an EvidenceRequest), distinct from the uploaded
 * EvidenceFile ("received"). camelCase on the wire; status is a snake_case string.
 */

import { EvidenceFile } from './audit.models';

export type EvidenceRequestStatus = 'requested' | 'received' | 'waived';

export const EVIDENCE_REQUEST_STATUSES: readonly EvidenceRequestStatus[] = ['requested', 'received', 'waived'];

/** Why a document is requested — a general review document or evidence backing a finding. */
export type EvidenceRequestPurpose = 'review_document' | 'finding_evidence';

export interface EvidenceRequest {
  id: string;
  auditId: string;
  checklistItemId: string | null;
  /** The finding this evidence backs (when purpose is finding_evidence). */
  exceptionId: string | null;
  purpose: EvidenceRequestPurpose;
  title: string;
  documentType: string | null;
  requestedByUserId: string;
  /** The auditee the document is requested from. */
  requestedFromUserId: string;
  requestedOn: string;
  dueDate: string | null;
  status: EvidenceRequestStatus;
  receivedByUserId: string | null;
  receivedAt: string | null;
  waiveReason: string | null;
  notes: string | null;
  isOverdue: boolean;
  /** Documents the auditee has uploaded against this request. */
  files: EvidenceFile[];
  version: string;
}

export interface RequestEvidenceRequest {
  requestedFromUserId: string;
  title: string;
  documentType?: string | null;
  dueDate?: string | null;
  notes?: string | null;
  checklistItemId?: string | null;
  exceptionId?: string | null;
  purpose?: EvidenceRequestPurpose | null;
}
