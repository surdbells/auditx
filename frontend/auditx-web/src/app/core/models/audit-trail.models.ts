/**
 * M11 — Audit Trail & Evidence Integrity models (camelCase, mirroring the
 * backend contract).
 *
 * `actorType` is serialized by the backend as snake_case (e.g. `user`,
 * `system`, `itandt_support`); the `*Json` fields carry raw JSON strings the
 * UI pretty-prints on demand.
 */

export interface AuditTrailEntry {
  id: string;
  eventType: string;
  targetObjectType: string;
  targetObjectId: string | null;
  actorUserId: string | null;
  actorType: string;
  actorSystemLabel: string | null;
  occurredAtUtc: string;
  originatingTimezone: string | null;
  beforeStateJson: string | null;
  afterStateJson: string | null;
  requestContextJson: string | null;
  eventPayloadJson: string | null;
}

export interface FlaggedEvidence {
  id: string;
  auditId: string;
  originalFilename: string;
  mimeType: string;
  sizeBytes: number;
  sha256Hash: string;
  uploadedAt: string;
  uploadedBy: string;
}

/**
 * Filter for the audit-trail query. Field names are camelCase here; the
 * service maps them to the snake_case query-param names the backend expects.
 */
export interface AuditTrailFilter {
  actorUserId?: string;
  eventType?: string;
  targetObjectType?: string;
  targetObjectId?: string;
  /** ISO datetime string. */
  dateFrom?: string;
  /** ISO datetime string. */
  dateTo?: string;
}
