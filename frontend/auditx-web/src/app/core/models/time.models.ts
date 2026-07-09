/**
 * P0-B — Time tracking models. Hours logged against an audit (with an optional
 * checklist-item link) drive budget-vs-actual and utilisation reporting.
 * camelCase on the wire; `category` is a snake_case string.
 */

export type TimeCategory =
  | 'fieldwork'
  | 'review'
  | 'reporting'
  | 'planning'
  | 'administration';

/** All selectable categories, in display order. */
export const TIME_CATEGORIES: readonly TimeCategory[] = [
  'fieldwork',
  'review',
  'reporting',
  'planning',
  'administration',
];

export interface TimeEntry {
  id: string;
  auditId: string;
  userId: string;
  checklistItemId: string | null;
  workDate: string;
  hours: number;
  category: TimeCategory;
  notes: string | null;
  version: string;
}

export interface CategoryHours {
  category: TimeCategory;
  hours: number;
}

export interface UserHours {
  userId: string;
  hours: number;
}

/** Budget-vs-actual + composition summary for one audit's logged time. */
export interface TimeSummary {
  auditId: string;
  budgetedHours: number | null;
  actualHours: number;
  varianceHours: number | null;
  percentConsumed: number | null;
  entryCount: number;
  contributorCount: number;
  byCategory: CategoryHours[];
  byUser: UserHours[];
}

/* ---- Request payloads ---- */

export interface LogTimeRequest {
  workDate: string;
  hours: number;
  category: TimeCategory;
  checklistItemId?: string | null;
  notes?: string | null;
}

export interface UpdateTimeRequest {
  workDate: string;
  hours: number;
  category: TimeCategory;
  checklistItemId?: string | null;
  notes?: string | null;
  version: string;
}

export interface SetAuditBudgetRequest {
  budgetedHours: number | null;
  version: string;
}
