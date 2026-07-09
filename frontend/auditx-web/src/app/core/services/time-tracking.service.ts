import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { ApiService } from './api.service';
import {
  Audit,
  LogTimeRequest,
  SetAuditBudgetRequest,
  TimeEntry,
  TimeSummary,
  UpdateTimeRequest,
} from '../models';

/**
 * Typed client for the P0-B time-tracking endpoints.
 *
 * Time entries are a separate aggregate — logging/amending/deleting does NOT
 * change the audit's rowversion. Only {@link setBudget} mutates the audit (it
 * returns the fresh {@link Audit}), so callers should reload the audit after it.
 * The amend/delete endpoints are NOT nested under /audits.
 */
@Injectable({ providedIn: 'root' })
export class TimeTrackingService {
  private readonly api = inject(ApiService);

  list(auditId: string): Observable<TimeEntry[]> {
    return this.api.get<TimeEntry[]>(`/audits/${auditId}/time-entries`);
  }

  summary(auditId: string): Observable<TimeSummary> {
    return this.api.get<TimeSummary>(`/audits/${auditId}/time-entries/summary`);
  }

  log(auditId: string, body: LogTimeRequest): Observable<TimeEntry> {
    return this.api.post<TimeEntry>(`/audits/${auditId}/time-entries`, body);
  }

  update(id: string, body: UpdateTimeRequest): Observable<TimeEntry> {
    return this.api.patch<TimeEntry>(`/time-entries/${id}`, body);
  }

  delete(id: string, version: string): Observable<void> {
    return this.api.deleteVoid(`/time-entries/${id}?version=${encodeURIComponent(version)}`);
  }

  /** Sets/clears the audit's budgeted hours; returns the fresh audit (version bumped). */
  setBudget(auditId: string, body: SetAuditBudgetRequest): Observable<Audit> {
    return this.api.patch<Audit>(`/audits/${auditId}/budget`, body);
  }
}
