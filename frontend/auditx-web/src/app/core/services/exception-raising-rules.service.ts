import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { ApiService } from './api.service';
import {
  CreateExceptionRaisingRuleRequest,
  ExceptionRaisingRule,
  UpdateExceptionRaisingRuleRequest,
} from '../models';

/** Typed client for the institution-configurable exception-raising-rule endpoints. */
@Injectable({ providedIn: 'root' })
export class ExceptionRaisingRulesService {
  private readonly api = inject(ApiService);

  list(): Observable<ExceptionRaisingRule[]> {
    return this.api.get<ExceptionRaisingRule[]>('/exception-raising-rules');
  }

  create(body: CreateExceptionRaisingRuleRequest): Observable<ExceptionRaisingRule> {
    return this.api.post<ExceptionRaisingRule>('/exception-raising-rules', body);
  }

  update(id: string, body: UpdateExceptionRaisingRuleRequest): Observable<ExceptionRaisingRule> {
    return this.api.patch<ExceptionRaisingRule>(`/exception-raising-rules/${id}`, body);
  }
}
