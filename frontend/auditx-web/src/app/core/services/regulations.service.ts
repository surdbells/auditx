import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { ApiService } from './api.service';
import {
  PagedResult,
  Regulation,
  RegulationListItem,
  RegulationQuery,
  RegisterRegulationRequest,
  SetRegulationStatusRequest,
  UpdateRegulationRequest,
} from '../models';

/** Typed client for the P1-B regulation / compliance register. Reads=ViewControls, writes=ManageControls. */
@Injectable({ providedIn: 'root' })
export class RegulationsService {
  private readonly api = inject(ApiService);

  list(query: RegulationQuery): Observable<PagedResult<RegulationListItem>> {
    return this.api.get<PagedResult<RegulationListItem>>('/regulations', {
      category: query.category,
      includeRetired: query.includeRetired,
      search: query.search,
      page: query.page,
      pageSize: query.pageSize,
    });
  }

  getById(id: string): Observable<Regulation> {
    return this.api.get<Regulation>(`/regulations/${id}`);
  }

  register(body: RegisterRegulationRequest): Observable<Regulation> {
    return this.api.post<Regulation>('/regulations', body);
  }

  update(id: string, body: UpdateRegulationRequest): Observable<Regulation> {
    return this.api.patch<Regulation>(`/regulations/${id}`, body);
  }

  setStatus(id: string, body: SetRegulationStatusRequest): Observable<Regulation> {
    return this.api.post<Regulation>(`/regulations/${id}/status`, body);
  }

  delete(id: string, version: string): Observable<void> {
    return this.api.deleteVoid(`/regulations/${id}?version=${encodeURIComponent(version)}`);
  }
}
