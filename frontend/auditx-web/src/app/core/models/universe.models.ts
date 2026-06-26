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
  /** Dimension-name → inherent score. */
  inherentScores: Record<string, number>;
  /** Dimension-name → residual score. */
  residualScores: Record<string, number>;
  compositeInherentScore: number | null;
  compositeResidualScore: number | null;
  lastAuditedAt: string | null;
  version: number;
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
}

export interface UpdateEntityRequest {
  name: string;
  entityType: string;
  description?: string | null;
  ownerUserId?: string | null;
  parentEntityId?: string | null;
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
  cursor?: string | null;
  limit?: number;
}

/** Filter for the risk-dimensions list. */
export type RiskDimensionFilter = 'true' | 'false' | 'all';
