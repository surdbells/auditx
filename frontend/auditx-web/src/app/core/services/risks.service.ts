import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { ApiService } from './api.service';
import {
  ChangeRiskStatusRequest,
  PagedResult,
  RegisterRiskRequest,
  Risk,
  RiskListItem,
  RiskQuery,
  UpdateRiskRequest,
} from '../models';

/** Typed client for the P1-A enterprise risk-register endpoints. Reads=ViewRisk, writes=ManageRisk. */
@Injectable({ providedIn: 'root' })
export class RisksService {
  private readonly api = inject(ApiService);

  list(query: RiskQuery): Observable<PagedResult<RiskListItem>> {
    return this.api.get<PagedResult<RiskListItem>>('/risks', {
      status: query.status,
      category: query.category,
      owner: query.owner,
      band: query.band,
      includeClosed: query.includeClosed,
      search: query.search,
      page: query.page,
      pageSize: query.pageSize,
    });
  }

  getById(id: string): Observable<Risk> {
    return this.api.get<Risk>(`/risks/${id}`);
  }

  register(body: RegisterRiskRequest): Observable<Risk> {
    return this.api.post<Risk>('/risks', body);
  }

  update(id: string, body: UpdateRiskRequest): Observable<Risk> {
    return this.api.patch<Risk>(`/risks/${id}`, body);
  }

  transition(id: string, body: ChangeRiskStatusRequest): Observable<Risk> {
    return this.api.post<Risk>(`/risks/${id}/transition`, body);
  }

  delete(id: string, version: string): Observable<void> {
    return this.api.deleteVoid(`/risks/${id}?version=${encodeURIComponent(version)}`);
  }
}
