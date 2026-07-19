import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { ApiService } from './api.service';
import { EngagementBoardItem, EngagementJourney } from '../models';

/** Typed client for the Engagement Lifecycle module (the audit-journey navigator). */
@Injectable({ providedIn: 'root' })
export class EngagementService {
  private readonly api = inject(ApiService);

  /** The caller's engagements with their stage + next action(s). Optional status filter. */
  board(status?: string): Observable<EngagementBoardItem[]> {
    return this.api.get<EngagementBoardItem[]>('/engagements', { status });
  }

  /** The full lifecycle journey for one engagement. */
  journey(auditId: string): Observable<EngagementJourney> {
    return this.api.get<EngagementJourney>(`/engagements/${auditId}/journey`);
  }
}
