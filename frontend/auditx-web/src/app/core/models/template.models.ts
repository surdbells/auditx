import type { ExceptionSeverity } from './exception.models';

/** Lifecycle state of an audit template. */
export type TemplateStatus = 'draft' | 'published' | 'archived';

/**
 * Response capture type for a checklist item. Verdict types capture a Pass/Fail/N-A conclusion;
 * value types capture a typed value (with an optional verdict for the exception workflow).
 */
export type ResponseType =
  | 'pass_fail_na'
  | 'yes_no'
  | 'text'
  | 'numeric'
  | 'date'
  | 'rating'
  | 'multiple_choice';

/** Response types that capture a typed value rather than only a verdict. */
export const VALUE_RESPONSE_TYPES: readonly ResponseType[] = [
  'text',
  'numeric',
  'date',
  'rating',
  'multiple_choice',
];

/** Lightweight row for the templates list view. */
export interface TemplateListItem {
  id: string;
  name: string;
  auditType: string;
  description: string;
  status: TemplateStatus;
  currentVersion: number;
  itemCount: number;
}

/** A single checklist item within a template. */
export interface TemplateItem {
  id: string;
  prompt: string;
  referenceNotes: string;
  responseType: ResponseType;
  sectionName: string;
  orderIndex: number;
  isRequired: boolean;
  defaultAssignmentRuleJson: string | null;
  /** Per-type config, e.g. `{"ratingScaleId":"..."}` for a Rating item. Opaque JSON. */
  responseConfigJson: string | null;
  /** How severe a failure of this item is; when set, drives the default severity of any exception raised against it. */
  riskRating: ExceptionSeverity | null;
}

/** A point on a rating scale: the value an auditor picks, its label, and its 0-100 score. */
export interface RatingScalePoint {
  value: number;
  label: string;
  score: number;
}

/** A bank-configurable, reusable labelled scale for Rating-type checklist items. */
export interface RatingScale {
  id: string;
  name: string;
  description: string | null;
  isActive: boolean;
  pointsJson: string;
}

export interface CreateRatingScaleRequest {
  name: string;
  description: string | null;
  pointsJson: string;
}

export interface UpdateRatingScaleRequest {
  name?: string;
  description?: string | null;
  pointsJson?: string;
  isActive?: boolean;
}

/** A named grouping of template items. */
export interface TemplateSection {
  id: string;
  name: string;
  orderIndex: number;
}

/** Summary of a published version. */
export interface TemplateVersionSummary {
  id: string;
  versionNumber: number;
  publishedAt: string;
}

/** Full aggregate for a single template. */
export interface Template {
  id: string;
  name: string;
  auditType: string;
  description: string;
  status: TemplateStatus;
  currentVersion: number;
  clonedFromTemplateId: string | null;
  items: TemplateItem[];
  sections: TemplateSection[];
  versions: TemplateVersionSummary[];
}

/** An item as captured in a published version snapshot (no id / mutable identity). */
export interface TemplateItemSnapshot {
  prompt: string;
  referenceNotes: string;
  responseType: ResponseType;
  sectionName: string;
  orderIndex: number;
  isRequired: boolean;
  defaultAssignmentRuleJson: string | null;
  responseConfigJson: string | null;
  riskRating: ExceptionSeverity | null;
}

/** Full detail of one published version. */
export interface TemplateVersionDetail {
  versionNumber: number;
  publishedAt: string;
  items: TemplateItemSnapshot[];
}

/** A modified item between two versions. */
export interface TemplateDiffModification {
  prompt: string;
  before: TemplateItemSnapshot;
  after: TemplateItemSnapshot;
}

/** Diff between two published versions. */
export interface TemplateDiff {
  fromVersion: number;
  toVersion: number;
  added: TemplateItemSnapshot[];
  removed: TemplateItemSnapshot[];
  modified: TemplateDiffModification[];
}

/* ---- Request payloads ---- */

export interface CreateTemplateRequest {
  name: string;
  auditType: string;
  description: string;
}

export interface UpdateTemplateRequest {
  name: string;
  description: string;
}

export interface SaveTemplateItemRequest {
  prompt: string;
  referenceNotes: string;
  responseType: ResponseType;
  sectionName: string;
  isRequired: boolean;
  defaultAssignmentRuleJson: string | null;
  responseConfigJson?: string | null;
  riskRating?: ExceptionSeverity | null;
}

export interface ReorderItemsRequest {
  orderedItemIds: string[];
}

export interface ReorderSectionsRequest {
  orderedSectionNames: string[];
}

export interface CreateSectionRequest {
  name: string;
}

export interface RenameSectionRequest {
  currentName: string;
  newName: string;
}

export interface CloneTemplateRequest {
  newName: string;
}

export interface TemplateQuery {
  auditType?: string;
  /** Backend defaults to published; pass 'all' for every state. */
  status?: TemplateStatus | 'all' | '';
  search?: string;
  page?: number;
  pageSize?: number;
}
