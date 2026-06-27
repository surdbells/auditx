import { HttpClient, HttpParams, HttpResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../environments/environment';
import { ApiService } from './api.service';
import {
  AuditTrailEntry,
  AuditTrailFilter,
  CursorPage,
  FlaggedEvidence,
} from '../models';

/**
 * Typed client for the M11 Audit Trail & Evidence Integrity endpoints.
 *
 * The query/object-history/unflag/flagged calls go through `ApiService`, which
 * unwraps the `{ data }` envelope. The CSV export is different: it returns a
 * raw file (not an envelope) and carries the artefact hash in a response
 * header, so it talks to `HttpClient` directly and returns the full response.
 */
@Injectable({ providedIn: 'root' })
export class AuditTrailService {
  private readonly api = inject(ApiService);
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  query(
    filter: AuditTrailFilter,
    cursor?: string,
    limit?: number,
  ): Observable<CursorPage<AuditTrailEntry>> {
    return this.api.get<CursorPage<AuditTrailEntry>>('/audit-trail', {
      actor_user_id: filter.actorUserId,
      event_type: filter.eventType,
      target_object_type: filter.targetObjectType,
      target_object_id: filter.targetObjectId,
      date_from: filter.dateFrom,
      date_to: filter.dateTo,
      cursor,
      limit,
    });
  }

  objectHistory(
    type: string,
    id: string,
    cursor?: string,
    limit?: number,
  ): Observable<CursorPage<AuditTrailEntry>> {
    return this.api.get<CursorPage<AuditTrailEntry>>(
      `/audit-trail/object/${type}/${id}`,
      { cursor, limit },
    );
  }

  /**
   * Downloads the audit-trail CSV export as a file.
   *
   * Bypasses `ApiService.get` (which unwraps `{ data }` as JSON) and reads the
   * raw blob plus headers — `X-Content-SHA256` (artefact hash) and
   * `X-Row-Count` — so the caller can verify integrity and trigger the
   * browser download.
   */
  exportCsv(filter: AuditTrailFilter): Observable<HttpResponse<Blob>> {
    let params = new HttpParams();
    const candidates: Record<string, string | undefined> = {
      actor_user_id: filter.actorUserId,
      event_type: filter.eventType,
      target_object_type: filter.targetObjectType,
      target_object_id: filter.targetObjectId,
      date_from: filter.dateFrom,
      date_to: filter.dateTo,
    };
    for (const [key, value] of Object.entries(candidates)) {
      if (value !== null && value !== undefined && value !== '') {
        params = params.set(key, value);
      }
    }
    return this.http.get(`${this.baseUrl}/audit-trail/export`, {
      params,
      responseType: 'blob',
      observe: 'response',
      withCredentials: true,
    });
  }

  unflagEvidence(
    auditId: string,
    id: string,
    resolution: string,
  ): Observable<void> {
    return this.api.postVoid(
      `/audits/${auditId}/evidence/${id}/unflag`,
      { resolution },
    );
  }

  listFlagged(): Observable<FlaggedEvidence[]> {
    return this.api.get<FlaggedEvidence[]>('/evidence/flagged');
  }
}
