import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { ApiService } from './api.service';
import {
  CreateReportScheduleRequest,
  ReportSchedule,
  UpdateReportScheduleRequest,
} from '../models';

/**
 * Recurring report schedules (D3-C). A schedule auto-generates a standalone report on a cadence and emails it to
 * its recipients; the runner background job executes due schedules server-side. Managing requires ScheduleReports.
 */
@Injectable({ providedIn: 'root' })
export class ReportSchedulesService {
  private readonly api = inject(ApiService);

  /** All live schedules, newest first. */
  list(): Observable<ReportSchedule[]> {
    return this.api.get<ReportSchedule[]>('/report-schedules');
  }

  create(body: CreateReportScheduleRequest): Observable<ReportSchedule> {
    return this.api.post<ReportSchedule>('/report-schedules', body);
  }

  update(id: string, body: UpdateReportScheduleRequest): Observable<ReportSchedule> {
    return this.api.patch<ReportSchedule>(`/report-schedules/${id}`, body);
  }

  delete(id: string, version: string): Observable<void> {
    return this.api.deleteVoid(`/report-schedules/${id}?version=${encodeURIComponent(version)}`);
  }
}
