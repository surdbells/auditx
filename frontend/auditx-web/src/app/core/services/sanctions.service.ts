import { HttpClient, HttpResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

import { environment } from '../../../environments/environment';
import { ApiService } from './api.service';
import {
  ActivateGridVersionRequest,
  ApiResponse,
  CursorPage,
  DecideAppealRequest,
  FileAppealRequest,
  RecordDcDecisionRequest,
  RecordHrOutcomeRequest,
  RecordRecommendationRequest,
  ReferToDcRequest,
  SanctionsAppeal,
  SanctionsCase,
  SanctionsCaseListItem,
  SanctionsGridActionResult,
  SanctionsGridVersion,
  SaveGridVersionRequest,
  TriggerSanctionsRequest,
  VersionRequest,
} from '../models';

/**
 * Typed client for the M7 Sanctions & Disciplinary Grid endpoints.
 *
 * Every case/appeal mutation echoes the aggregate's `version`; the response is the
 * updated aggregate with a fresh version callers must refresh into local state.
 * Two endpoints are special:
 *
 *  - The dossier download returns a raw HTML file (not the `{ data }` envelope)
 *    and carries the artefact hash in the `X-Dossier-Sha256` header, so it talks
 *    to `HttpClient` directly (mirrors {@link AuditTrailService.exportCsv}).
 *  - Grid create/activate are maker-checker-gateable: a 200/201 carries the grid
 *    version, a 202 carries only `{ pendingActionId }`. {@link saveGrid} /
 *    {@link activateGrid} read the HTTP status to disambiguate.
 */
@Injectable({ providedIn: 'root' })
export class SanctionsService {
  private readonly api = inject(ApiService);
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  /* ---- Tracker + detail ---- */

  /** Cursor-paged case tracker (subject ALWAYS masked). */
  list(
    status?: string,
    cursor?: string | null,
    limit?: number,
  ): Observable<CursorPage<SanctionsCaseListItem>> {
    return this.api.get<CursorPage<SanctionsCaseListItem>>('/sanctions/cases', {
      status,
      cursor,
      limit,
    });
  }

  getById(id: string): Observable<SanctionsCase> {
    return this.api.get<SanctionsCase>(`/sanctions/cases/${id}`);
  }

  /** DC member queue: referred cases awaiting a committee decision. */
  dcQueue(
    cursor?: string | null,
    limit?: number,
  ): Observable<CursorPage<SanctionsCaseListItem>> {
    return this.api.get<CursorPage<SanctionsCaseListItem>>(
      '/sanctions/dc-queue',
      { cursor, limit },
    );
  }

  /* ---- Trigger (exception-scoped) ---- */

  trigger(
    exceptionId: string,
    body: TriggerSanctionsRequest,
  ): Observable<SanctionsCase> {
    return this.api.post<SanctionsCase>(
      `/exceptions/${exceptionId}/sanctions/trigger`,
      body,
    );
  }

  /* ---- Recommendation ---- */

  recordRecommendation(
    id: string,
    body: RecordRecommendationRequest,
  ): Observable<SanctionsCase> {
    return this.api.patch<SanctionsCase>(
      `/sanctions/cases/${id}/recommendation`,
      body,
    );
  }

  submit(id: string, body: VersionRequest): Observable<SanctionsCase> {
    return this.api.post<SanctionsCase>(`/sanctions/cases/${id}/submit`, body);
  }

  /* ---- Dossier (raw HTML file + X-Dossier-Sha256 header) ---- */

  /**
   * Generates the case dossier as a downloadable HTML file. Bypasses
   * `ApiService` (which unwraps `{ data }` as JSON) and reads the raw blob plus
   * the `X-Dossier-Sha256` header so the caller can surface integrity + trigger
   * the browser download.
   */
  generateDossier(id: string): Observable<HttpResponse<Blob>> {
    return this.http.post(
      `${this.baseUrl}/sanctions/cases/${id}/dossier`,
      {},
      {
        responseType: 'blob',
        observe: 'response',
        withCredentials: true,
      },
    );
  }

  /* ---- HR outcome / DC referral / DC decision ---- */

  recordHrOutcome(
    id: string,
    body: RecordHrOutcomeRequest,
  ): Observable<SanctionsCase> {
    return this.api.post<SanctionsCase>(
      `/sanctions/cases/${id}/hr-outcome`,
      body,
    );
  }

  referToDc(id: string, body: ReferToDcRequest): Observable<SanctionsCase> {
    return this.api.post<SanctionsCase>(`/sanctions/cases/${id}/dc-refer`, body);
  }

  recordDcDecision(
    id: string,
    body: RecordDcDecisionRequest,
  ): Observable<SanctionsCase> {
    return this.api.post<SanctionsCase>(
      `/sanctions/cases/${id}/dc-decision`,
      body,
    );
  }

  /* ---- Appeals ---- */

  fileAppeal(id: string, body: FileAppealRequest): Observable<SanctionsAppeal> {
    return this.api.post<SanctionsAppeal>(
      `/sanctions/cases/${id}/appeals`,
      body,
    );
  }

  decideAppeal(
    appealId: string,
    body: DecideAppealRequest,
  ): Observable<SanctionsAppeal> {
    return this.api.post<SanctionsAppeal>(
      `/sanctions/appeals/${appealId}/decision`,
      body,
    );
  }

  /* ---- Close ---- */

  close(id: string, body: VersionRequest): Observable<SanctionsCase> {
    return this.api.post<SanctionsCase>(`/sanctions/cases/${id}/close`, body);
  }

  /* ---- Grid ---- */

  getActiveGrid(): Observable<SanctionsGridVersion | null> {
    return this.api.get<SanctionsGridVersion | null>('/sanctions/grid');
  }

  /**
   * Saves a new grid draft. Returns `{ gridVersion }` when applied directly
   * (200/201) or `{ pendingActionId }` when maker-checker-gated (202). Reads the
   * HTTP status to disambiguate.
   */
  saveGrid(body: SaveGridVersionRequest): Observable<SanctionsGridActionResult> {
    return this.gridAction(
      this.http.post<
        ApiResponse<SanctionsGridVersion | { pendingActionId: string }>
      >(`${this.baseUrl}/sanctions/grid`, body, {
        withCredentials: true,
        observe: 'response',
      }),
    );
  }

  /**
   * Activates a grid version (reason >= 20 chars). Returns `{ gridVersion }`
   * when applied directly (200) or `{ pendingActionId }` when gated (202).
   */
  activateGrid(
    id: string,
    body: ActivateGridVersionRequest,
  ): Observable<SanctionsGridActionResult> {
    return this.gridAction(
      this.http.post<
        ApiResponse<SanctionsGridVersion | { pendingActionId: string }>
      >(`${this.baseUrl}/sanctions/grid/${id}/activate`, body, {
        withCredentials: true,
        observe: 'response',
      }),
    );
  }

  private gridAction(
    op: Observable<
      HttpResponse<ApiResponse<SanctionsGridVersion | { pendingActionId: string }>>
    >,
  ): Observable<SanctionsGridActionResult> {
    return op.pipe(
      map((res) => {
        const data = res.body?.data;
        if (res.status === 202) {
          const pending = data as { pendingActionId: string } | null;
          return { pendingActionId: pending?.pendingActionId };
        }
        return { gridVersion: data as SanctionsGridVersion };
      }),
    );
  }
}
