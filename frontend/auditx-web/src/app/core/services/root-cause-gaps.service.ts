import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

import { ApiService } from './api.service';
import { environment } from '../../../environments/environment';
import {
  AddGapRemediationRequest,
  ApiResponse,
  CloseRootCauseGapRequest,
  CompleteGapRemediationRequest,
  CreateRootCauseGapRequest,
  LinkExceptionToGapRequest,
  PagedResult,
  RootCauseGap,
  RootCauseGapListItem,
  RootCauseGapQuery,
  UpdateGapRemediationRequest,
  UpdateRootCauseGapRequest,
} from '../models';

/** Typed client for the P2 root-cause-gaps register. Reads = ViewExceptions, writes = ManageException. */
@Injectable({ providedIn: 'root' })
export class RootCauseGapsService {
  private readonly api = inject(ApiService);
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  list(query: RootCauseGapQuery): Observable<PagedResult<RootCauseGapListItem>> {
    return this.api.get<PagedResult<RootCauseGapListItem>>('/root-cause-gaps', {
      status: query.status,
      search: query.search,
      page: query.page,
      pageSize: query.pageSize,
    });
  }

  getById(id: string): Observable<RootCauseGap> {
    return this.api.get<RootCauseGap>(`/root-cause-gaps/${id}`);
  }

  create(body: CreateRootCauseGapRequest): Observable<RootCauseGap> {
    return this.api.post<RootCauseGap>('/root-cause-gaps', body);
  }

  update(id: string, body: UpdateRootCauseGapRequest): Observable<RootCauseGap> {
    return this.api.patch<RootCauseGap>(`/root-cause-gaps/${id}`, body);
  }

  close(id: string, body: CloseRootCauseGapRequest): Observable<RootCauseGap> {
    return this.api.post<RootCauseGap>(`/root-cause-gaps/${id}/close`, body);
  }

  reopen(id: string, version: string): Observable<RootCauseGap> {
    return this.api.post<RootCauseGap>(`/root-cause-gaps/${id}/reopen`, { version });
  }

  linkException(id: string, body: LinkExceptionToGapRequest): Observable<RootCauseGap> {
    return this.api.post<RootCauseGap>(`/root-cause-gaps/${id}/exceptions`, body);
  }

  /** DELETE returns the updated gap DTO (not 204), so read the enveloped body. */
  unlinkException(id: string, exceptionId: string, version: string): Observable<RootCauseGap> {
    return this.http
      .delete<ApiResponse<RootCauseGap>>(
        `${this.baseUrl}/root-cause-gaps/${id}/exceptions/${exceptionId}?version=${encodeURIComponent(version)}`,
        { withCredentials: true },
      )
      .pipe(map((r) => r.data));
  }

  // ---- Remediation plan ----

  addRemediation(id: string, body: AddGapRemediationRequest): Observable<RootCauseGap> {
    return this.api.post<RootCauseGap>(`/root-cause-gaps/${id}/remediations`, body);
  }

  updateRemediation(id: string, remediationId: string, body: UpdateGapRemediationRequest): Observable<RootCauseGap> {
    return this.api.patch<RootCauseGap>(`/root-cause-gaps/${id}/remediations/${remediationId}`, body);
  }

  completeRemediation(id: string, remediationId: string, body: CompleteGapRemediationRequest): Observable<RootCauseGap> {
    return this.api.post<RootCauseGap>(`/root-cause-gaps/${id}/remediations/${remediationId}/complete`, body);
  }

  reopenRemediation(id: string, remediationId: string, version: string): Observable<RootCauseGap> {
    return this.api.post<RootCauseGap>(`/root-cause-gaps/${id}/remediations/${remediationId}/reopen`, { version });
  }

  /** DELETE returns the updated gap DTO (not 204), so read the enveloped body. */
  removeRemediation(id: string, remediationId: string, version: string): Observable<RootCauseGap> {
    return this.http
      .delete<ApiResponse<RootCauseGap>>(
        `${this.baseUrl}/root-cause-gaps/${id}/remediations/${remediationId}?version=${encodeURIComponent(version)}`,
        { withCredentials: true },
      )
      .pipe(map((r) => r.data));
  }
}
