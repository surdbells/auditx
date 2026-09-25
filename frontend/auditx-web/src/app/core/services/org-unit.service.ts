import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { ApiService } from './api.service';
import {
  CreateOrgUnitRequest,
  OrgUnit,
  RenameOrgUnitRequest,
  ReparentOrgUnitRequest,
} from '../models';

/**
 * Typed client for the organisation-unit hierarchy endpoints (`/org-units`).
 *
 * Reads require `ViewUniverse`; every mutation requires `ManageUniverse`.
 * Reparent enforces an acyclic tree (409 `org_unit.cycle_detected`); create
 * enforces a unique code (409 `org_unit.code_taken`).
 */
@Injectable({ providedIn: 'root' })
export class OrgUnitService {
  private readonly api = inject(ApiService);

  list(includeArchived = false): Observable<OrgUnit[]> {
    return this.api.get<OrgUnit[]>('/org-units', { includeArchived });
  }

  create(body: CreateOrgUnitRequest): Observable<OrgUnit> {
    return this.api.post<OrgUnit>('/org-units', body);
  }

  rename(id: string, body: RenameOrgUnitRequest): Observable<OrgUnit> {
    return this.api.patch<OrgUnit>(`/org-units/${id}/rename`, body);
  }

  reparent(id: string, body: ReparentOrgUnitRequest): Observable<OrgUnit> {
    return this.api.patch<OrgUnit>(`/org-units/${id}/parent`, body);
  }

  /** Designate (or clear, with null) the user who heads this org unit — the reporting-line fallback. */
  setHead(id: string, headUserId: string | null): Observable<OrgUnit> {
    return this.api.patch<OrgUnit>(`/org-units/${id}/head`, { headUserId });
  }

  archive(id: string): Observable<OrgUnit> {
    return this.api.post<OrgUnit>(`/org-units/${id}/archive`, {});
  }

  restore(id: string): Observable<OrgUnit> {
    return this.api.post<OrgUnit>(`/org-units/${id}/restore`, {});
  }
}
