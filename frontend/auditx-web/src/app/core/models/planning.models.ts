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

/** One entity a plan item covers, and the (at most one live) audit launched for it. */
export interface PlanItemEntityLink {
  id: string;
  entityId: string;
  linkedAuditId: string | null;
  status: PlanItemStatus;
}

export interface PlanItem {
  id: string;
  auditType: string;
  plannedStartDate: string;
  plannedEndDate: string;
  estimatedEffortDays: number | null;
  assignedLeadUserId: string | null;
  /** Roll-up: in_progress if any entity is; completed only once every entity is; else planned/deferred. */
  status: PlanItemStatus;
  orderIndex: number;
  /** One or more entities this item covers — each gets its own independently-launched audit. */
  entityLinks: PlanItemEntityLink[];
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
  /** True when the plan's items may be turned into audits (approved plan, or a deployment that allows pre-approval launch). */
  canLaunchAudits: boolean;
  /** True when an Approved plan may still be edited directly via a "minor revision" (no re-approval) — off by default. */
  canApplyMinorRevision: boolean;
  /** Why the current material revision was requested, if any (set when the plan was re-opened for AC re-approval). */
  revisionReason: string | null;
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

/** Real checklist-completion progress of the audit launched for one entity of a plan item. */
export interface PlanItemProgress {
  planItemId: string;
  entityId: string;
  linkedAuditId: string | null;
  auditStatus: string | null;
  totalChecklistItems: number;
  respondedChecklistItems: number;
  percentComplete: number;
}

/** Execution roll-up for a plan. */
export interface PlanExecution {
  totalItems: number;
  countsByStatus: Record<string, number>;
  percentComplete: number;
  behindSchedule: PlanItem[];
  /** Aggregate checklist completion across every audit the plan's items have launched. */
  totalChecklistItems: number;
  respondedChecklistItems: number;
  checklistPercentComplete: number;
  itemProgress: PlanItemProgress[];
}

/* ---- Request payloads ---- */

export interface SavePlanRequest {
  periodLabel: string;
  periodStart: string;
  periodEnd: string;
}

export interface AddPlanItemRequest {
  entityIds: string[];
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
  /** Optional context for why the change is requested — shown to the AC chair for a material revision. */
  reason?: string;
}

export interface PlanDecisionRequest {
  decision: PlanDecisionKind;
  detail?: string;
  comments?: string;
}

export interface PlanQuery {
  status?: PlanStatus | '';
  page?: number;
  pageSize?: number;
}
