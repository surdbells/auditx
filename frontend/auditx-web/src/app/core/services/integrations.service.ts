import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { ApiService } from './api.service';
import {
  CreateIntegrationRequest,
  Integration,
  IntegrationHealth,
  IntegrationTestResult,
  UpdateIntegrationRequest,
} from '../models';

/** Typed client for the M14 Integrations endpoints. */
@Injectable({ providedIn: 'root' })
export class IntegrationsService {
  private readonly api = inject(ApiService);

  list(): Observable<Integration[]> {
    return this.api.get<Integration[]>('/integrations');
  }

  getById(id: string): Observable<Integration> {
    return this.api.get<Integration>(`/integrations/${id}`);
  }

  health(id: string): Observable<IntegrationHealth> {
    return this.api.get<IntegrationHealth>(`/integrations/${id}/health`);
  }

  create(body: CreateIntegrationRequest): Observable<Integration> {
    return this.api.post<Integration>('/integrations', body);
  }

  update(
    id: string,
    body: UpdateIntegrationRequest,
  ): Observable<Integration> {
    return this.api.patch<Integration>(`/integrations/${id}`, body);
  }

  /** Deactivates the integration (DELETE → 204). */
  deactivate(id: string): Observable<void> {
    return this.api.deleteVoid(`/integrations/${id}`);
  }

  test(id: string): Observable<IntegrationTestResult> {
    return this.api.post<IntegrationTestResult>(`/integrations/${id}/test`);
  }
}
