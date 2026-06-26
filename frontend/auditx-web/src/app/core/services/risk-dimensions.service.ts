import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { ApiService } from './api.service';
import {
  CreateRiskDimensionRequest,
  RiskDimension,
  RiskDimensionFilter,
  UpdateRiskDimensionRequest,
} from '../models';

/** Typed client for the M3 risk-dimension configuration endpoints. */
@Injectable({ providedIn: 'root' })
export class RiskDimensionsService {
  private readonly api = inject(ApiService);

  /** `active` filters by state: `'true'` (default) / `'false'` / `'all'`. */
  list(active: RiskDimensionFilter = 'true'): Observable<RiskDimension[]> {
    return this.api.get<RiskDimension[]>('/risk-dimensions', { active });
  }

  create(body: CreateRiskDimensionRequest): Observable<RiskDimension> {
    return this.api.post<RiskDimension>('/risk-dimensions', body);
  }

  update(
    id: string,
    body: UpdateRiskDimensionRequest,
  ): Observable<RiskDimension> {
    return this.api.patch<RiskDimension>(`/risk-dimensions/${id}`, body);
  }
}
