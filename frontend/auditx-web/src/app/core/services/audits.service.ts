import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { ApiService } from './api.service';
import {
  AddChecklistItemRequest,
  AddTeamMemberRequest,
  Audit,
  AuditCounts,
  AuditListItem,
  AuditQuery,
  CancelAuditRequest,
  CreateAuditRequest,
  CursorPage,
  TransferLeadRequest,
  TransitionAuditRequest,
  UpdateAuditRequest,
  UpdateChecklistItemRequest,
} from '../models';

/** Typed client for the M4 Audit Lifecycle endpoints. */
@Injectable({ providedIn: 'root' })
export class AuditsService {
  private readonly api = inject(ApiService);

  list(query: AuditQuery): Observable<CursorPage<AuditListItem>> {
    return this.api.get<CursorPage<AuditListItem>>('/audits', {
      status: query.status,
      auditType: query.auditType,
      lead: query.lead,
      planItem: query.planItem,
      cursor: query.cursor,
      limit: query.limit,
    });
  }

  counts(): Observable<AuditCounts> {
    return this.api.get<AuditCounts>('/audits/counts');
  }

  getById(id: string): Observable<Audit> {
    return this.api.get<Audit>(`/audits/${id}`);
  }

  create(body: CreateAuditRequest): Observable<Audit> {
    return this.api.post<Audit>('/audits', body);
  }

  update(id: string, body: UpdateAuditRequest): Observable<Audit> {
    return this.api.patch<Audit>(`/audits/${id}`, body);
  }

  /* ---- Lifecycle ---- */

  transition(id: string, body: TransitionAuditRequest): Observable<Audit> {
    return this.api.post<Audit>(`/audits/${id}/transition`, body);
  }

  cancel(id: string, body: CancelAuditRequest): Observable<Audit> {
    return this.api.post<Audit>(`/audits/${id}/cancel`, body);
  }

  /* ---- Team ---- */

  addTeamMember(id: string, body: AddTeamMemberRequest): Observable<Audit> {
    return this.api.post<Audit>(`/audits/${id}/team`, body);
  }

  removeTeamMember(
    id: string,
    membershipId: string,
    version: string,
  ): Observable<void> {
    return this.api.deleteVoid(
      `/audits/${id}/team/${membershipId}?version=${encodeURIComponent(version)}`,
    );
  }

  transferLead(id: string, body: TransferLeadRequest): Observable<Audit> {
    return this.api.patch<Audit>(`/audits/${id}/transfer-lead`, body);
  }

  /* ---- Checklist ---- */

  addChecklistItem(
    id: string,
    body: AddChecklistItemRequest,
  ): Observable<Audit> {
    return this.api.post<Audit>(`/audits/${id}/checklist/items`, body);
  }

  updateChecklistItem(
    id: string,
    itemId: string,
    body: UpdateChecklistItemRequest,
  ): Observable<Audit> {
    return this.api.patch<Audit>(
      `/audits/${id}/checklist/items/${itemId}`,
      body,
    );
  }

  removeChecklistItem(
    id: string,
    itemId: string,
    version: string,
  ): Observable<void> {
    return this.api.deleteVoid(
      `/audits/${id}/checklist/items/${itemId}?version=${encodeURIComponent(version)}`,
    );
  }
}
