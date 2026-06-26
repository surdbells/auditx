/** Lifecycle state of an audit template. */
export type TemplateStatus = 'draft' | 'published' | 'archived';

/** Response capture type for a template item. Currently a single supported value. */
export type ResponseType = 'pass_fail_na';

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
}

export interface ReorderItemsRequest {
  orderedItemIds: string[];
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
  cursor?: string | null;
  limit?: number;
}
