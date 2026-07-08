import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

import { environment } from '../../../environments/environment';
import { ApiService } from './api.service';
import {
  AddChecklistItemRequest,
  AddTeamMemberRequest,
  ApiResponse,
  AssignItemRequest,
  Audit,
  AuditCounts,
  AuditListItem,
  AuditQuery,
  BulkReassignRequest,
  CancelAuditRequest,
  ChecklistProgress,
  ChecklistResponse,
  CreateAuditRequest,
  CursorPage,
  EvidenceFile,
  FailJudgementRequest,
  FailWithoutExceptionResult,
  ResponseHistoryEntry,
  ReviewSummary,
  SubmitResponseRequest,
  TransferLeadRequest,
  TransitionAuditRequest,
  UpdateAuditRequest,
  UpdateChecklistItemRequest,
} from '../models';

/** Typed client for the M4 Audit Lifecycle + M5 Execution endpoints. */
@Injectable({ providedIn: 'root' })
export class AuditsService {
  private readonly api = inject(ApiService);
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  list(query: AuditQuery): Observable<CursorPage<AuditListItem>> {
    return this.api.get<CursorPage<AuditListItem>>('/audits', {
      status: query.status,
      auditType: query.auditType,
      lead: query.lead,
      planItem: query.planItem,
      cursor: query.cursor,
      limit: query.limit,
    });
  }

  counts(): Observable<AuditCounts> {
    return this.api.get<AuditCounts>('/audits/counts');
  }

  getById(id: string): Observable<Audit> {
    return this.api.get<Audit>(`/audits/${id}`);
  }

  create(body: CreateAuditRequest): Observable<Audit> {
    return this.api.post<Audit>('/audits', body);
  }

  update(id: string, body: UpdateAuditRequest): Observable<Audit> {
    return this.api.patch<Audit>(`/audits/${id}`, body);
  }

  /* ---- Lifecycle ---- */

  transition(id: string, body: TransitionAuditRequest): Observable<Audit> {
    return this.api.post<Audit>(`/audits/${id}/transition`, body);
  }

  cancel(id: string, body: CancelAuditRequest): Observable<Audit> {
    return this.api.post<Audit>(`/audits/${id}/cancel`, body);
  }

  /* ---- Team ---- */

  addTeamMember(id: string, body: AddTeamMemberRequest): Observable<Audit> {
    return this.api.post<Audit>(`/audits/${id}/team`, body);
  }

  removeTeamMember(
    id: string,
    membershipId: string,
    version: string,
  ): Observable<void> {
    return this.api.deleteVoid(
      `/audits/${id}/team/${membershipId}?version=${encodeURIComponent(version)}`,
    );
  }

  transferLead(id: string, body: TransferLeadRequest): Observable<Audit> {
    return this.api.patch<Audit>(`/audits/${id}/transfer-lead`, body);
  }

  /* ---- Checklist ---- */

  addChecklistItem(
    id: string,
    body: AddChecklistItemRequest,
  ): Observable<Audit> {
    return this.api.post<Audit>(`/audits/${id}/checklist/items`, body);
  }

  updateChecklistItem(
    id: string,
    itemId: string,
    body: UpdateChecklistItemRequest,
  ): Observable<Audit> {
    return this.api.patch<Audit>(
      `/audits/${id}/checklist/items/${itemId}`,
      body,
    );
  }

  removeChecklistItem(
    id: string,
    itemId: string,
    version: string,
  ): Observable<void> {
    return this.api.deleteVoid(
      `/audits/${id}/checklist/items/${itemId}?version=${encodeURIComponent(version)}`,
    );
  }

  /** Re-stamp checklist item order (drag-and-drop). Sends the full ordered id list. */
  reorderChecklistItems(
    id: string,
    body: { orderedItemIds: string[]; version: string },
  ): Observable<Audit> {
    return this.api.post<Audit>(`/audits/${id}/checklist/items/reorder`, body);
  }

  /* =====================================================================
   * M5 — Execution / Fieldwork
   * ===================================================================== */

  /* ---- Responses ---- */

  /**
   * Submit (or draft) a response. Returns the ChecklistResponse — NOT the audit —
   * so callers must reload the audit + progress afterwards to refresh `version`.
   */
  submitResponse(
    id: string,
    itemId: string,
    body: SubmitResponseRequest,
  ): Observable<ChecklistResponse> {
    return this.api.post<ChecklistResponse>(
      `/audits/${id}/items/${itemId}/responses`,
      body,
    );
  }

  getResponse(
    id: string,
    itemId: string,
  ): Observable<ChecklistResponse | null> {
    return this.api.get<ChecklistResponse | null>(
      `/audits/${id}/items/${itemId}/responses`,
    );
  }

  discardDraft(id: string, itemId: string, version: string): Observable<void> {
    return this.api.deleteVoid(
      `/audits/${id}/items/${itemId}/responses/draft?version=${encodeURIComponent(version)}`,
    );
  }

  getResponseHistory(
    id: string,
    itemId: string,
  ): Observable<ResponseHistoryEntry[]> {
    return this.api.get<ResponseHistoryEntry[]>(
      `/audits/${id}/items/${itemId}/responses/history`,
    );
  }

  getChecklistProgress(id: string): Observable<ChecklistProgress> {
    return this.api.get<ChecklistProgress>(`/audits/${id}/checklist/progress`);
  }

  /* ---- Assignment ---- */

  assignItem(
    id: string,
    itemId: string,
    body: AssignItemRequest,
  ): Observable<Audit> {
    return this.api.patch<Audit>(
      `/audits/${id}/items/${itemId}/assignment`,
      body,
    );
  }

  bulkReassign(id: string, body: BulkReassignRequest): Observable<Audit> {
    return this.api.post<Audit>(`/audits/${id}/items/bulk-reassign`, body);
  }

  /* ---- Review ---- */

  getFailWithoutException(
    id: string,
  ): Observable<FailWithoutExceptionResult> {
    return this.api.get<FailWithoutExceptionResult>(
      `/audits/${id}/review/fail-without-exception`,
    );
  }

  getReviewSummary(id: string): Observable<ReviewSummary> {
    return this.api.get<ReviewSummary>(`/audits/${id}/review/summary`);
  }

  recordFailJudgement(
    id: string,
    itemId: string,
    body: FailJudgementRequest,
  ): Observable<Audit> {
    return this.api.post<Audit>(
      `/audits/${id}/items/${itemId}/fail-judgement`,
      body,
    );
  }

  /* ---- Evidence (separate aggregate — NO audit version) ---- */

  /** Multipart upload. There is no ApiService multipart helper, so post directly. */
  uploadEvidence(
    id: string,
    responseId: string,
    file: File,
  ): Observable<EvidenceFile> {
    const form = new FormData();
    form.append('file', file);
    return this.http
      .post<ApiResponse<EvidenceFile>>(
        `${this.baseUrl}/audits/${id}/responses/${responseId}/evidence`,
        form,
        { withCredentials: true },
      )
      .pipe(map((r) => r.data));
  }

  listEvidence(id: string, responseId: string): Observable<EvidenceFile[]> {
    return this.api.get<EvidenceFile[]>(
      `/audits/${id}/responses/${responseId}/evidence`,
    );
  }

  /** Downloads the raw binary as a Blob (caller triggers the browser download). */
  downloadEvidence(id: string, evidenceId: string): Observable<Blob> {
    return this.http.get(
      `${this.baseUrl}/audits/${id}/evidence/${evidenceId}`,
      { responseType: 'blob', withCredentials: true },
    );
  }

  deleteEvidence(
    id: string,
    evidenceId: string,
    reason: string,
  ): Observable<void> {
    return this.api.deleteVoid(
      `/audits/${id}/evidence/${evidenceId}?reason=${encodeURIComponent(reason)}`,
    );
  }
}
