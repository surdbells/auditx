/**
 * Organisation-structure models — the org-unit hierarchy (department / business
 * unit tree) that audit-universe entities and users are assigned to.
 *
 * The tree is a flat adjacency list: each unit carries its `parentOrgUnitId`
 * (null at the root). The backend enforces an acyclic graph and unique codes.
 */

/** A node in the organisation hierarchy. */
export interface OrgUnit {
  id: string;
  name: string;
  code: string;
  parentOrgUnitId: string | null;
  /** The user who heads this unit — the reporting-line fallback for members without an explicit manager. */
  headUserId: string | null;
  /** Shown as "Inactive": kept for history, not offered for new assignments. */
  isArchived: boolean;
  /** What kind of unit this is — a code from the org_unit_type reference list (legal entity, business unit, …). */
  unitType?: string | null;
}

/* ---- Request payloads ---- */

export interface CreateOrgUnitRequest {
  name: string;
  code: string;
  parentOrgUnitId?: string | null;
  unitType?: string | null;
}

export interface RenameOrgUnitRequest {
  name: string;
}

export interface ReparentOrgUnitRequest {
  parentOrgUnitId: string | null;
}
