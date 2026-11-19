/**
 * An audit's engagement records (GET audits/{id}/engagement) as the signed-in user may see them: the engagement
 * letter, the team's independence declarations and the kickoff and exit meeting records. The auditee sees a letter
 * only once issued, never the independence record (null for them), and the meeting records in full.
 */
export interface EngagementRecords {
  auditId: string;
  isSelfAssessment: boolean;
  /** May write the letter and record the meetings (the audit team, with ManageAudit). */
  canManage: boolean;
  isAuditee: boolean;
  /** The auditee may acknowledge the issued letter now. */
  canAcknowledge: boolean;
  letter: EngagementLetter | null;
  independence: TeamIndependence | null;
  meetings: AuditMeeting[];
  /** Completed draft reports an exit meeting can name (for those who record meetings). */
  draftReports: DraftReportOption[];
  /** The audit's version after the last change (declaring and accepting change it). */
  auditVersion: string;
  /** Approval of the programme before fieldwork (null for a self-assessment and for the auditee). */
  programme?: ProgrammeApproval | null;
}

export type EngagementLetterStatus = 'draft' | 'issued' | 'acknowledged';

export interface EngagementLetter {
  id: string;
  status: EngagementLetterStatus;
  revision: number;
  body: string;
  draftedBy: string;
  draftedAt: string;
  issuedBy: string | null;
  issuedAt: string | null;
  acknowledgedBy: string | null;
  acknowledgedAt: string | null;
  acknowledgementComment: string | null;
  previousRevisions: EngagementLetterRevision[];
  version: string;
}

export interface EngagementLetterRevision {
  revision: number;
  body: string;
  issuedBy: string | null;
  issuedAt: string | null;
  acknowledgedBy: string | null;
  acknowledgedAt: string | null;
  acknowledgementComment: string | null;
}

/** Approval of the audit programme before fieldwork. `canApprove`: the signed-in user may approve it now. */
export interface ProgrammeApproval {
  required: boolean;
  approved: boolean;
  approvedBy: string | null;
  approvedAt: string | null;
  note: string | null;
  canApprove: boolean;
}

export type IndependenceMemberStatus = 'pending' | 'declared' | 'conflict_disclosed' | 'conflict_accepted';

export interface TeamIndependence {
  /** Fieldwork waits for every member's declaration (institution setting). */
  required: boolean;
  complete: boolean;
  outstanding: number;
  /** The signed-in user is on the team and may declare now. */
  canDeclare: boolean;
  /** The signed-in user may accept a disclosed conflict (never their own). */
  canDecide: boolean;
  members: IndependenceMember[];
}

export interface IndependenceMember {
  userId: string;
  teamRole: string;
  status: IndependenceMemberStatus;
  declarationId: string | null;
  declaredAt: string | null;
  /** Shown only to the declarant and to those who decide on conflicts. */
  conflictDetails: string | null;
  reviewedBy: string | null;
  reviewedAt: string | null;
  reviewNote: string | null;
}

export type MeetingKind = 'kickoff' | 'exit';

export interface AuditMeeting {
  id: string;
  kind: MeetingKind;
  heldAt: string;
  attendeeUserIds: string[];
  externalAttendees: string[];
  minutes: string;
  agreedPoints: string[];
  reportId: string | null;
  reportVersionNumber: number | null;
  recordedBy: string;
  recordedAt: string;
  lastEditedBy: string | null;
  lastEditedAt: string | null;
  version: string;
}

export interface DraftReportOption {
  id: string;
  versionNumber: number;
  completedAt: string | null;
}

export interface RecordMeetingRequest {
  heldAt: string;
  attendeeUserIds: string[];
  externalAttendees: string[];
  minutes: string;
  agreedPoints: string[];
  reportId: string | null;
  meetingVersion: string | null;
}
