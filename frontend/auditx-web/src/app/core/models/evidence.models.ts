/**
 * P2-D — expected / requested evidence: the "requested" side (an EvidenceRequest), distinct from the uploaded
 * EvidenceFile ("received"). camelCase on the wire; status is a snake_case string.
 */

export type EvidenceRequestStatus = 'requested' | 'received' | 'waived';

export const EVIDENCE_REQUEST_STATUSES: readonly EvidenceRequestStatus[] = ['requested', 'received', 'waived'];

export interface EvidenceRequest {
  id: string;
  auditId: string;
  checklistItemId: string | null;
  title: string;
  documentType: string | null;
  requestedByUserId: string;
  requestedOn: string;
  dueDate: string | null;
  status: EvidenceRequestStatus;
  receivedByUserId: string | null;
  receivedAt: string | null;
  waiveReason: string | null;
  notes: string | null;
  isOverdue: boolean;
  version: string;
}

export interface RequestEvidenceRequest {
  checklistItemId?: string | null;
  title: string;
  documentType?: string | null;
  dueDate?: string | null;
  notes?: string | null;
}
