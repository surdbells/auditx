/**
 * M3 — Audit Universe models.
 *
 * Risk scores are keyed by risk-dimension name; composite scores are the
 * weighted roll-up the backend computes from the per-dimension scores.
 */

/** A scorable node in the audit universe (business unit, process, system, …). */
export interface Entity {
  id: string;
  entityType: string;
  name: string;
  description: string | null;
  parentEntityId: string | null;
  ownerUserId: string | null;
  /** Owning organisation unit (department / business unit); null when unassigned. */
  orgUnitId: string | null;
  /** Dimension-name → inherent score. */
  inherentScores: Record<string, number>;
  /** Dimension-name → residual score. */
  residualScores: Record<string, number>;
  compositeInherentScore: number | null;
  compositeResidualScore: number | null;
  lastAuditedAt: string | null;
  /** How many times per year this entity is expected to be audited (coverage target); null when unset. */
  expectedAuditsPerYear: number | null;
  version: number;
  /** True when the entity has been archived (only returned when the list is asked to include archived). */
  isArchived?: boolean;
  /** When it was archived; null / absent for a live entity. */
  archivedAt?: string | null;
}

/** Lightweight row for the entities list (same shape as Entity from the API). */
export type EntityListItem = Entity;

/** A configurable risk dimension used when scoring entities. */
export interface RiskDimension {
  id: string;
  name: string;
  weight: number;
  scaleMin: number;
  scaleMax: number;
  isActive: boolean;
  scaleLabelOverridesJson: string | null;
}

/** A single audit-trail entry (reused for risk-score history). */
export interface AuditTrailEntryView {
  id: string;
  eventType: string;
  targetObjectType: string;
  targetObjectId: string;
  actorUserId: string | null;
  actorType: string;
  occurredAtUtc: string;
  beforeStateJson: string | null;
  afterStateJson: string | null;
  eventPayloadJson: string | null;
}

/** Result of a CSV bulk-import (atomic: created is 0 when any error is present). */
export interface BulkImportResult {
  created: number;
  errors: BulkImportError[];
}

export interface BulkImportError {
  row: number;
  field: string;
  message: string;
}

/* ---- Request payloads ---- */

export interface CreateEntityRequest {
  name: string;
  entityType: string;
  description?: string | null;
  parentEntityId?: string | null;
  ownerUserId?: string | null;
  orgUnitId?: string | null;
  expectedAuditsPerYear?: number | null;
}

export interface UpdateEntityRequest {
  name: string;
  entityType: string;
  description?: string | null;
  ownerUserId?: string | null;
  parentEntityId?: string | null;
  orgUnitId?: string | null;
  expectedAuditsPerYear?: number | null;
  version: number;
}

export interface SaveRiskScoresRequest {
  inherentScores?: Record<string, number>;
  residualScores?: Record<string, number>;
  version: number;
}

export interface BulkImportRequest {
  csvContent: string;
}

export interface CreateRiskDimensionRequest {
  name: string;
  weight: number;
  scaleMin?: number;
  scaleMax?: number;
  scaleLabelOverridesJson?: string | null;
}

export interface UpdateRiskDimensionRequest {
  weight?: number;
  scaleMin?: number;
  scaleMax?: number;
  isActive?: boolean;
  scaleLabelOverridesJson?: string | null;
}

export interface EntityQuery {
  entityType?: string;
  owner?: string;
  archived?: boolean;
  search?: string;
  page?: number;
  pageSize?: number;
}

/** Filter for the risk-dimensions list. */
export type RiskDimensionFilter = 'true' | 'false' | 'all';

/* ---- Entity audit history ---- */

/** Where an audit of an entity came from. */
export type EntityAuditOrigin = 'plan' | 'ad_hoc' | 'self_assessment';

/** One audit in an entity's history, with its checklist outcome and findings. */
export interface EntityAuditHistoryItem {
  auditId: string;
  name: string;
  auditType: string;
  /** Audit status as the API sends it: draft | planned | in_progress | under_review | completed | cancelled. */
  status: string;
  isSelfAssessment: boolean;
  startDate: string;
  targetEndDate: string;
  actualEndDate: string | null;
  leadUserId: string;
  leadName: string | null;
  auditeeUserId: string;
  auditeeName: string | null;
  origin: EntityAuditOrigin;
  /** The annual plan's period label when the audit was launched from a plan item. */
  planPeriodLabel: string | null;
  checklistItems: number;
  respondedItems: number;
  passed: number;
  failed: number;
  notApplicable: number;
  findings: number;
  openFindings: number;
  criticalFindings: number;
  highFindings: number;
  mediumFindings: number;
  lowFindings: number;
  reportsIssued: number;
}

/** Every audit ever linked to one entity, newest first, with headline figures. */
export interface EntityAuditHistory {
  entityId: string;
  entityName: string;
  entityType: string;
  isArchived: boolean;
  lastAuditedAt: string | null;
  expectedAuditsPerYear: number | null;
  /** Independent audits (self-assessments are counted separately). */
  totalAudits: number;
  completedAudits: number;
  inFlightAudits: number;
  selfAssessments: number;
  totalFindings: number;
  openFindings: number;
  audits: EntityAuditHistoryItem[];
}
