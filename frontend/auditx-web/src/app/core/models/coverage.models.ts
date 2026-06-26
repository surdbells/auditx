/**
 * M3 — Coverage analytics models.
 */

/** A universe entity that has not been audited within the requested window. */
export interface NotAuditedRow {
  id: string;
  entityType: string;
  name: string;
  lastAuditedAt: string | null;
}

/** A high-residual-risk entity with a coverage gap. */
export interface HighRiskGapRow {
  id: string;
  entityType: string;
  name: string;
  compositeResidualScore: number | null;
  lastAuditedAt: string | null;
}

/** Coverage matrix: rows × columns grid of audit counts. */
export interface CoverageMatrix {
  rows: string[];
  columns: string[];
  cells: number[][];
}
