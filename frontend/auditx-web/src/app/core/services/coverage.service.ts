import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { ApiService } from './api.service';
import {
  CoverageMatrix,
  HighRiskGapRow,
  NotAuditedRow,
} from '../models';

/** Typed client for the M3 Coverage Analytics endpoints. */
@Injectable({ providedIn: 'root' })
export class CoverageService {
  private readonly api = inject(ApiService);

  notAuditedSince(
    months: number,
    entityType?: string,
  ): Observable<NotAuditedRow[]> {
    return this.api.get<NotAuditedRow[]>(
      '/coverage-analytics/not-audited-since',
      { months, entityType },
    );
  }

  highRiskGaps(
    months: number,
    entityType?: string,
  ): Observable<HighRiskGapRow[]> {
    return this.api.get<HighRiskGapRow[]>(
      '/coverage-analytics/high-risk-gaps',
      { months, entityType },
    );
  }

  matrix(window?: string): Observable<CoverageMatrix> {
    return this.api.get<CoverageMatrix>('/coverage-analytics/matrix', {
      window,
    });
  }
}
