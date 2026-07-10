import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { ApiService } from './api.service';
import {
  Control,
  ControlListItem,
  ControlQuery,
  PagedResult,
  RegisterControlRequest,
  SetControlStatusRequest,
  UpdateControlRequest,
} from '../models';

/** Typed client for the P1-B internal-controls register. Reads=ViewControls, writes=ManageControls. */
@Injectable({ providedIn: 'root' })
export class ControlsService {
  private readonly api = inject(ApiService);

  list(query: ControlQuery): Observable<PagedResult<ControlListItem>> {
    return this.api.get<PagedResult<ControlListItem>>('/controls', {
      type: query.type,
      effectiveness: query.effectiveness,
      owner: query.owner,
      includeRetired: query.includeRetired,
      search: query.search,
      page: query.page,
      pageSize: query.pageSize,
    });
  }

  getById(id: string): Observable<Control> {
    return this.api.get<Control>(`/controls/${id}`);
  }

  register(body: RegisterControlRequest): Observable<Control> {
    return this.api.post<Control>('/controls', body);
  }

  update(id: string, body: UpdateControlRequest): Observable<Control> {
    return this.api.patch<Control>(`/controls/${id}`, body);
  }

  setStatus(id: string, body: SetControlStatusRequest): Observable<Control> {
    return this.api.post<Control>(`/controls/${id}/status`, body);
  }

  delete(id: string, version: string): Observable<void> {
    return this.api.deleteVoid(`/controls/${id}?version=${encodeURIComponent(version)}`);
  }
}
