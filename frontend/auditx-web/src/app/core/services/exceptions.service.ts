import { HttpClient, HttpParams, HttpResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

import { environment } from '../../../environments/environment';
import { ApiService } from './api.service';
import {
  ApiResponse,
  ApproveMapResult,
  ChangeSeverityRequest,
  CloseExceptionRequest,
  PagedResult,
  EvidenceFile,
  Exception,
  ExceptionHistoryEntry,
  ExceptionListItem,
  ExceptionQuery,
  FindingControlLink,
  FindingLinks,
  FindingRegulationLink,
  AddVerificationRequest,
  ManagementResponseRequest,
  RaiseExceptionRequest,
  ReassignOwnerRequest,
  ReasonVersionRequest,
  SubmitMapRequest,
  VersionRequest,
} from '../models';

/**
 * Typed client for the M6 Exceptions & Management Action Plan (MAP) endpoints.
 *
 * Every exception mutation echoes the Exception's `version`; the response is the
 * updated Exception with a fresh version that callers must refresh into local
 * state. Evidence list/upload/download carry NO version. `approve` returns the
 * updated Exception (200) or, when maker-checker-gated, a 202 with a
 * `pendingActionId` — {@link approveMap} surfaces which path occurred.
 */
@Injectable({ providedIn: 'root' })
export class ExceptionsService {
  private readonly api = inject(ApiService);
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  /* ---- Raise / list / read ---- */

  raise(auditId: string, body: RaiseExceptionRequest): Observable<Exception> {
    return this.api.post<Exception>(`/audits/${auditId}/exceptions`, body);
  }

  /** Per-audit list (used on the audit-detail page). */
  listForAudit(
    auditId: string,
    status?: string,
  ): Observable<ExceptionListItem[]> {
    return this.api.get<ExceptionListItem[]>(
      `/audits/${auditId}/exceptions`,
      { status },
    );
  }

  /** Cross-audit offset-paged tracker. */
  list(query: ExceptionQuery): Observable<PagedResult<ExceptionListItem>> {
    return this.api.get<PagedResult<ExceptionListItem>>('/exceptions', {
      status: query.status,
      severity: query.severity,
      owner: query.owner,
      entity: query.entity,
      audit: query.audit,
      plan: query.plan,
      category: query.category,
      search: query.search,
      recurrence: query.recurrence,
      overdue: query.overdue,
      raisedFrom: query.raisedFrom,
      raisedTo: query.raisedTo,
      page: query.page,
      pageSize: query.pageSize,
    });
  }

  /** Downloads the cross-audit finding register as a CSV blob (same filters as the tracker; SHA-256 on a header). */
  exportRegister(query: ExceptionQuery): Observable<HttpResponse<Blob>> {
    let params = new HttpParams();
    const set = (key: string, value: unknown) => {
      if (value !== undefined && value !== null && value !== '') {
        params = params.set(key, String(value));
      }
    };
    set('status', query.status);
    set('severity', query.severity);
    set('owner', query.owner);
    set('entity', query.entity);
    set('audit', query.audit);
    set('plan', query.plan);
    set('category', query.category);
    set('search', query.search);
    set('recurrence', query.recurrence);
    set('overdue', query.overdue);
    set('raisedFrom', query.raisedFrom);
    set('raisedTo', query.raisedTo);
    return this.http.get(`${this.baseUrl}/exceptions/export`, {
      params,
      responseType: 'blob',
      observe: 'response',
      withCredentials: true,
    });
  }

  getById(id: string): Observable<Exception> {
    return this.api.get<Exception>(`/exceptions/${id}`);
  }

  getHistory(id: string): Observable<ExceptionHistoryEntry[]> {
    return this.api.get<ExceptionHistoryEntry[]>(`/exceptions/${id}/history`);
  }

  /* ---- Control / regulation links (P1-B) ---- */

  /** The controls + regulations linked to this finding (ViewExceptions). */
  getLinks(id: string): Observable<FindingLinks> {
    return this.api.get<FindingLinks>(`/exceptions/${id}/links`);
  }

  /** Links a control to this finding (ManageException). Idempotent. */
  linkControl(id: string, controlId: string): Observable<FindingControlLink> {
    return this.api.post<FindingControlLink>(`/exceptions/${id}/controls`, { controlId });
  }

  /** Removes a control link (ManageException). No version — the link is a join row. */
  unlinkControl(id: string, controlId: string): Observable<void> {
    return this.api.deleteVoid(`/exceptions/${id}/controls/${controlId}`);
  }

  /** Links a regulation to this finding (ManageException). Idempotent. */
  linkRegulation(id: string, regulationId: string): Observable<FindingRegulationLink> {
    return this.api.post<FindingRegulationLink>(`/exceptions/${id}/regulations`, { regulationId });
  }

  /** Removes a regulation link (ManageException). No version — the link is a join row. */
  unlinkRegulation(id: string, regulationId: string): Observable<void> {
    return this.api.deleteVoid(`/exceptions/${id}/regulations/${regulationId}`);
  }

  /* ---- Management ---- */

  changeSeverity(
    id: string,
    body: ChangeSeverityRequest,
  ): Observable<Exception> {
    return this.api.patch<Exception>(`/exceptions/${id}/severity`, body);
  }

  reassignOwner(
    id: string,
    body: ReassignOwnerRequest,
  ): Observable<Exception> {
    return this.api.patch<Exception>(`/exceptions/${id}/owner`, body);
  }

  cancel(id: string, body: ReasonVersionRequest): Observable<Exception> {
    return this.api.post<Exception>(`/exceptions/${id}/cancel`, body);
  }

  /* ---- Management response / follow-up verification / reopen (P2-B) ---- */

  recordManagementResponse(id: string, body: ManagementResponseRequest): Observable<Exception> {
    return this.api.post<Exception>(`/exceptions/${id}/management-response`, body);
  }

  addVerification(id: string, body: AddVerificationRequest): Observable<Exception> {
    return this.api.post<Exception>(`/exceptions/${id}/verifications`, body);
  }

  reopen(id: string, body: ReasonVersionRequest): Observable<Exception> {
    return this.api.post<Exception>(`/exceptions/${id}/reopen`, body);
  }

  /* ---- MAP lifecycle ---- */

  submitMap(id: string, body: SubmitMapRequest): Observable<Exception> {
    return this.api.post<Exception>(`/exceptions/${id}/map`, body);
  }

  /**
   * Approves the MAP. Returns `{ exception }` when applied directly (200) or
   * `{ pendingActionId }` when maker-checker-gated (202, status stays
   * map_submitted). Reads the HTTP status to disambiguate.
   */
  approveMap(id: string, body: VersionRequest): Observable<ApproveMapResult> {
    return this.http
      .post<ApiResponse<Exception | { pendingActionId: string }>>(
        `${this.baseUrl}/exceptions/${id}/map/approve`,
        body,
        { withCredentials: true, observe: 'response' },
      )
      .pipe(
        map((res) => {
          const data = res.body?.data;
          if (res.status === 202) {
            const pending = data as { pendingActionId: string } | null;
            return { pendingActionId: pending?.pendingActionId };
          }
          return { exception: data as Exception };
        }),
      );
  }

  rejectMap(id: string, body: ReasonVersionRequest): Observable<Exception> {
    return this.api.post<Exception>(`/exceptions/${id}/map/reject`, body);
  }

  markMapComplete(id: string, body: VersionRequest): Observable<Exception> {
    return this.api.post<Exception>(`/exceptions/${id}/map/mark-complete`, body);
  }

  returnForEvidence(
    id: string,
    body: ReasonVersionRequest,
  ): Observable<Exception> {
    return this.api.post<Exception>(
      `/exceptions/${id}/return-for-evidence`,
      body,
    );
  }

  /** Marks a single MAP action complete. */
  markActionComplete(
    id: string,
    actionId: string,
    body: VersionRequest,
  ): Observable<Exception> {
    return this.api.patch<Exception>(
      `/exceptions/${id}/map/actions/${actionId}`,
      body,
    );
  }

  /* ---- Closure / CIA ---- */

  close(id: string, body: CloseExceptionRequest): Observable<Exception> {
    return this.api.post<Exception>(`/exceptions/${id}/close`, body);
  }

  ciaCountersign(id: string, body: VersionRequest): Observable<Exception> {
    return this.api.post<Exception>(`/exceptions/${id}/cia-countersign`, body);
  }

  /* ---- Per-action evidence (NO version) ---- */

  listActionEvidence(
    id: string,
    actionId: string,
  ): Observable<EvidenceFile[]> {
    return this.api.get<EvidenceFile[]>(
      `/exceptions/${id}/map/actions/${actionId}/evidence`,
    );
  }

  /** Multipart upload (field `file`); no version. */
  uploadActionEvidence(
    id: string,
    actionId: string,
    file: File,
  ): Observable<EvidenceFile> {
    const form = new FormData();
    form.append('file', file);
    return this.http
      .post<ApiResponse<EvidenceFile>>(
        `${this.baseUrl}/exceptions/${id}/map/actions/${actionId}/evidence`,
        form,
        { withCredentials: true },
      )
      .pipe(map((r) => r.data));
  }

  /** Downloads the raw evidence binary as a Blob (the Exception carries auditId). */
  downloadEvidence(auditId: string, evidenceId: string): Observable<Blob> {
    return this.http.get(
      `${this.baseUrl}/audits/${auditId}/evidence/${evidenceId}`,
      { responseType: 'blob', withCredentials: true },
    );
  }
}
