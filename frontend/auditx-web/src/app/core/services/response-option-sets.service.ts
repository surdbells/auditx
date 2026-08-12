import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { ApiService } from './api.service';
import {
  ResponseOptionSet,
  UpdateResponseOptionSetRequest,
} from '../models';

/**
 * Typed client for organisation-defined conclusion option sets (M4). Reads require ViewTemplates; writes require
 * ManageTemplates. `responseType` is the snake_case type key (e.g. 'pass_fail_na').
 */
@Injectable({ providedIn: 'root' })
export class ResponseOptionSetsService {
  private readonly api = inject(ApiService);

  list(): Observable<ResponseOptionSet[]> {
    return this.api.get<ResponseOptionSet[]>('/response-option-sets');
  }

  get(responseType: string): Observable<ResponseOptionSet> {
    return this.api.get<ResponseOptionSet>(`/response-option-sets/${responseType}`);
  }

  update(responseType: string, body: UpdateResponseOptionSetRequest): Observable<ResponseOptionSet> {
    return this.api.put<ResponseOptionSet>(`/response-option-sets/${responseType}`, body);
  }

  reset(responseType: string): Observable<ResponseOptionSet> {
    return this.api.post<ResponseOptionSet>(`/response-option-sets/${responseType}/reset`, {});
  }
}
