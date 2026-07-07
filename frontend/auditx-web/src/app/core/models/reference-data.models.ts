/**
 * Managed reference data — the small, admin-editable lookups (audit types,
 * exception categories, …) that back dropdowns across the app.
 *
 * Each category holds an ordered list of items whose stored value is `code`
 * and whose human label is `label`. Reads are authenticated-only (every role);
 * writes require the `ManageConfiguration` permission.
 */

/** A reference-data category, e.g. `audit_type` → "Audit types". */
export interface ReferenceDataCategory {
  code: string;
  label: string;
}

/** A single managed item within a category. */
export interface ReferenceDataItem {
  id: string;
  category: string;
  code: string;
  label: string;
  description?: string;
  sortOrder: number;
  isActive: boolean;
}

/* ---- Request payloads ---- */

export interface CreateReferenceDataItemRequest {
  code: string;
  label: string;
  description?: string;
  sortOrder: number;
}

export interface UpdateReferenceDataItemRequest {
  label: string;
  description?: string;
  sortOrder: number;
}
