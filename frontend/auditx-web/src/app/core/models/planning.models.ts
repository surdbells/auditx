/**
 * M3 — Annual Planning models.
 *
 * DateOnly fields (periodStart/End, plannedStart/EndDate) are ISO `yyyy-MM-dd`.
 */

/** Lifecycle state of an annual plan. */
export type PlanStatus =
  | 'draft'
  | 'submitted'
  | 'revisions_requested'
  | 'approved'
  | 'revision_submitted'
  | 'closed';

/** Lifecycle state of an individual plan item. */
export type PlanItemStatus =
  | 'planned'
  | 'in_progress'
  | 'completed'
  | 'deferred';

/** The AC Chair's decision on a submitted plan. */
export type PlanDecisionKind =
  | 'approved'
  | 'revisions_requested'
  | 'rejected';

/** Kind of revision submitted against an approved/in-flight plan. */
export type PlanRevisionKind = 'minor' | 'material';

export interface PlanDecision {
  decision: PlanDecisionKind;
  detail: string | null;
  comments: string[];
  decidedBy: string | null;
  decidedAt: string | null;
}

export interface PlanItem {
  id: string;
  entityId: string;
  auditType: string;
  plannedStartDate: string;
  plannedEndDate: string;
  estimatedEffortDays: number | null;
  assignedLeadUserId: string | null;
  linkedAuditId: string | null;
  status: PlanItemStatus;
  orderIndex: number;
}

/** Locates a plan item within its owning plan — backs the audit → plan deep link. */
export interface PlanItemLocator {
  planItemId: string;
  planId: string;
  periodLabel: string;
  itemStatus: PlanItemStatus;
}

export interface ReorderPlanItemsRequest {
  orderedItemIds: string[];
}

export interface Plan {
  id: string;
  periodLabel: string;
  periodStart: string;
  periodEnd: string;
  status: PlanStatus;
  submittedAt: string | null;
  approvedAt: string | null;
  approvalDecision: PlanDecision | null;
  items: PlanItem[];
}

/** Lightweight row for the plans list. */
export interface PlanListItem {
  id: string;
  periodLabel: string;
  periodStart: string;
  periodEnd: string;
  status: PlanStatus;
  itemCount: number;
}

/** Execution roll-up for a plan. */
export interface PlanExecution {
  totalItems: number;
  countsByStatus: Record<string, number>;
  percentComplete: number;
  behindSchedule: PlanItem[];
}

/* ---- Request payloads ---- */

export interface SavePlanRequest {
  periodLabel: string;
  periodStart: string;
  periodEnd: string;
}

export interface AddPlanItemRequest {
  entityId: string;
  auditType: string;
  plannedStartDate: string;
  plannedEndDate: string;
  estimatedEffortDays?: number | null;
  assignedLeadUserId?: string | null;
}

export interface SubmitRevisionRequest {
  kind: PlanRevisionKind;
  itemId?: string;
  newStartDate?: string;
  newEndDate?: string;
}

export interface PlanDecisionRequest {
  decision: PlanDecisionKind;
  detail?: string;
  comments?: string;
}

export interface PlanQuery {
  status?: PlanStatus | '';
  cursor?: string | null;
  limit?: number;
}
