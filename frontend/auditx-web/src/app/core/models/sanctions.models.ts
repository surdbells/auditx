/**
 * M7 — Sanctions & Disciplinary Grid models.
 *
 * The API emits and expects snake_case enum values (e.g. `recommendation_drafted`).
 * `version` is a base64 rowversion echoed on every mutation for optimistic
 * concurrency: after each successful mutation the response carries a fresh
 * `version` that the next mutation must send.
 *
 * Subject confidentiality: list rows ALWAYS mask the subject (`subjectMasked`
 * true, `subjectUserId` null). The detail view exposes the subject only to
 * case-team members; otherwise `subjectMasked` stays true.
 */

/** Lifecycle status of a sanctions case (snake_case on the wire). */
export type SanctionsCaseStatus =
  | 'recommendation_drafted'
  | 'recommendation_submitted'
  | 'hr_outcome_recorded'
  | 'dc_referral'
  | 'dc_decision_recorded'
  | 'appealed'
  | 'appeal_decision_recorded'
  | 'closed';

/** HR authority's outcome on a recommended sanction. */
export type HrOutcomeType = 'imposed' | 'declined' | 'modified' | 'dc_referral';

/** Disciplinary committee's decision on a referred case. */
export type DcDecisionType = 'uphold' | 'modify' | 'dismiss';

/** Appeals authority's outcome on a filed appeal. */
export type AppealOutcome = 'confirm' | 'modify' | 'overturn';

/** Lifecycle status of an appeal. */
export type AppealStatus = 'filed' | 'in_review' | 'decided';

/** Full sanctions-case aggregate (detail view). */
export interface SanctionsCase {
  id: string;
  exceptionId: string;
  subjectUserId: string | null;
  subjectMasked: boolean;
  status: SanctionsCaseStatus;
  category: string | null;
  severity: string;
  isRecurrence: boolean;
  recommendation: string | null;
  gridConsultedVersion: number | null;
  gridRecommendedRange: string | null;
  withinGridRange: boolean;
  deviationReason: string | null;
  hrOutcomeJson: string | null;
  dcDecisionJson: string | null;
  triggeredBy: string;
  triggeredAt: string;
  recommendedBy: string | null;
  recommendedAt: string | null;
  submittedAt: string | null;
  hrOutcomeAt: string | null;
  dcDecisionAt: string | null;
  closedBy: string | null;
  closedAt: string | null;
  version: string;
  teamMemberUserIds: string[];
  latestAppealId?: string | null;
  latestAppealStatus?: string | null;
  latestAppealVersion?: string | null;
}

/** Lightweight row for the sanctions tracker / DC queue (subject always masked). */
export interface SanctionsCaseListItem {
  id: string;
  exceptionId: string;
  subjectUserId: string | null;
  subjectMasked: boolean;
  status: SanctionsCaseStatus;
  category: string | null;
  severity: string;
  isRecurrence: boolean;
  triggeredAt: string;
}

/** A version of the sanctions disciplinary grid. */
export interface SanctionsGridVersion {
  id: string;
  versionNumber: number;
  gridDefinitionJson: string;
  isActive: boolean;
  activationReason: string | null;
  createdByUserId: string;
  createdAtUtc: string;
  activatedBy: string | null;
  activatedAt: string | null;
  version: string;
}

/** An appeal filed against a sanctions case. */
export interface SanctionsAppeal {
  id: string;
  sanctionsCaseId: string;
  appellantUserId: string;
  routedToUserId: string;
  basis: string;
  status: AppealStatus;
  decisionJson: string | null;
  filedAt: string;
  decidedAt: string | null;
  version: string;
}

/**
 * Parsed shape of {@link SanctionsGridVersion.gridDefinitionJson}: keyed by
 * `<category>|<severity>|<true|false>` (recurrence flag), each cell carrying a
 * recommended sanction range.
 */
export interface GridDefinition {
  cells: Record<string, GridCell>;
}

export interface GridCell {
  recommended_range: string;
}

/* ---- Request payloads ---- */

/**
 * Version-only mutation body (`{ version }`). Reuses the shared `VersionRequest`
 * declared in {@link ./exception.models} — re-exported via the models barrel.
 */

export interface TriggerSanctionsRequest {
  subjectUserId: string | null;
}

export interface RecordRecommendationRequest {
  recommendation: string;
  deviationReason?: string | null;
  version: string;
}

export interface RecordHrOutcomeRequest {
  outcomeType: HrOutcomeType;
  detail: string;
  evidenceFileId?: string | null;
  version: string;
}

export interface ReferToDcRequest {
  referralReason: string;
  version: string;
}

export interface RecordDcDecisionRequest {
  decision: DcDecisionType;
  rationale: string;
  votingRecord?: string | null;
  version: string;
}

export interface FileAppealRequest {
  basis: string;
  evidenceFileId?: string | null;
  version: string;
}

export interface DecideAppealRequest {
  outcome: AppealOutcome;
  rationale: string;
  version: string;
}

/** Body for POST /sanctions/grid — the full grid definition as a JSON string. */
export interface SaveGridVersionRequest {
  gridDefinition: string;
}

/** Body for POST /sanctions/grid/{id}/activate (reason >= 20 chars). */
export interface ActivateGridVersionRequest {
  activationReason: string;
}

/**
 * Result of a maker-checker-gateable grid action: either the new/updated grid
 * version (applied directly, 200/201) or a `pendingActionId` when gated (202).
 */
export interface SanctionsGridActionResult {
  gridVersion?: SanctionsGridVersion | null;
  pendingActionId?: string | null;
}
