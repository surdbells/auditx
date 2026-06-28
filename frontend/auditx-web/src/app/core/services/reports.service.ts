import { HttpClient, HttpResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../environments/environment';
import { ApiService } from './api.service';
import {
  ActivateReportTemplateRequest,
  CreateReportTemplateRequest,
  CursorPage,
  DistributeReportRequest,
  DistributeReportResult,
  GenerateReportRequest,
  GenerateReportResult,
  Report,
  ReportDistribution,
  ReportHashVerification,
  ReportListItem,
  ReportTemplate,
} from '../models';

/**
 * Typed client for the M8 Reports endpoints.
 *
 * Generation is asynchronous: {@link generate} POSTs and returns the 202 body
 * `{ reportId, status }`; the caller then polls {@link getById} until the status
 * settles on `completed` / `failed`.
 *
 * The artefact download is special: it returns a raw file (not the `{ data }`
 * envelope) and, on an integrity failure, a 500. It therefore talks to
 * `HttpClient` directly and returns the full response so the caller can trigger
 * the browser download (mirrors {@link AuditTrailService.exportCsv}).
 */
@Injectable({ providedIn: 'root' })
export class ReportsService {
  private readonly api = inject(ApiService);
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  /* ---- Generation + status ---- */

  /**
   * Requests a new report version. Returns the 202 body `{ reportId, status }`
   * (status = `pending`); the caller polls {@link getById} for completion.
   */
  generate(
    auditId: string,
    body: GenerateReportRequest,
  ): Observable<GenerateReportResult> {
    return this.api.post<GenerateReportResult>(
      `/audits/${auditId}/reports`,
      body,
    );
  }

  /** Cursor-paginated version list (newest first) for an audit. */
  listForAudit(
    auditId: string,
    cursor?: string | null,
    limit?: number,
  ): Observable<CursorPage<ReportListItem>> {
    return this.api.get<CursorPage<ReportListItem>>(
      `/audits/${auditId}/reports`,
      { cursor, limit },
    );
  }

  /** Single report metadata/status surface — POLL this for generation status. */
  getById(id: string): Observable<Report> {
    return this.api.get<Report>(`/reports/${id}`);
  }

  /* ---- Download (raw file; 500 on integrity failure) ---- */

  /**
   * Downloads a produced report artefact as a file. Bypasses `ApiService`
   * (which unwraps `{ data }` as JSON) and reads the raw blob plus headers so
   * the caller can trigger the browser download. On an integrity failure the
   * API returns 500, surfaced to the caller's error handler.
   */
  download(id: string, format: string): Observable<HttpResponse<Blob>> {
    return this.http.get(`${this.baseUrl}/reports/${id}/download`, {
      params: { format },
      responseType: 'blob',
      observe: 'response',
      withCredentials: true,
    });
  }

  /** Stored-vs-recomputed integrity check for a report artefact. */
  verifyHash(id: string): Observable<ReportHashVerification> {
    return this.api.get<ReportHashVerification>(`/reports/${id}/verify-hash`);
  }

  /* ---- Distribution ---- */

  distribute(
    id: string,
    body: DistributeReportRequest,
  ): Observable<DistributeReportResult> {
    return this.api.post<DistributeReportResult>(
      `/reports/${id}/distribute`,
      body,
    );
  }

  distributions(
    id: string,
    cursor?: string | null,
    limit?: number,
  ): Observable<CursorPage<ReportDistribution>> {
    return this.api.get<CursorPage<ReportDistribution>>(
      `/reports/${id}/distributions`,
      { cursor, limit },
    );
  }

  /* ---- Templates ---- */

  listTemplates(): Observable<ReportTemplate[]> {
    return this.api.get<ReportTemplate[]>('/report-templates');
  }

  createTemplate(
    body: CreateReportTemplateRequest,
  ): Observable<ReportTemplate> {
    return this.api.post<ReportTemplate>('/report-templates', body);
  }

  activateTemplate(
    id: string,
    body: ActivateReportTemplateRequest,
  ): Observable<ReportTemplate> {
    return this.api.patch<ReportTemplate>(
      `/report-templates/${id}/activate`,
      body,
    );
  }
}
