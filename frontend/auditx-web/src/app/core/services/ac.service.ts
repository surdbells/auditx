import { HttpClient, HttpResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { environment } from '../../../environments/environment';
import { ApiService } from './api.service';
import {
  AcActionItem,
  AcComment,
  AcDashboard,
  AcPack,
  AcPackAnalytics,
  AcPackDistribution,
  AcPackDistributionResult,
  AcPackGenerationResult,
  AcPackListItem,
  AcPackQuery,
  AddAcCommentRequest,
  ApproveAcPackRequest,
  CreateAcActionItemRequest,
  FindingVisibilityRestriction,
  GenerateAcPackRequest,
  PagedResult,
  RestrictFindingVisibilityRequest,
  UpdateAcActionItemRequest,
  UpdateAcPackCiaTextRequest,
} from '../models';

/**
 * Typed client for the M13 Audit-Committee Workspace endpoints.
 *
 * Pack generation is asynchronous: {@link generatePack} POSTs and returns the
 * 202 body `{ acPackId, status }`; the caller polls {@link getPack} until the
 * status settles on `approved` / `distributed` / `failed`.
 *
 * The artefact download is special: it returns a raw file (not the `{ data }`
 * envelope). It therefore talks to `HttpClient` directly and returns the full
 * response so the caller can trigger the browser download (mirrors
 * {@link ReportsService.download}).
 */
@Injectable({ providedIn: 'root' })
export class AcService {
  private readonly api = inject(ApiService);
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  /* ---- AC packs ---- */

  /**
   * Requests a new AC pack. Returns the 202 body `{ acPackId, status }`; the
   * caller polls {@link getPack} for completion.
   */
  generatePack(
    body: GenerateAcPackRequest,
  ): Observable<AcPackGenerationResult> {
    return this.api.post<AcPackGenerationResult>('/ac-packs/generate', body);
  }

  /** Page of AC packs, optionally filtered by status. */
  listPacks(query: AcPackQuery = {}): Observable<PagedResult<AcPackListItem>> {
    return this.api.get<PagedResult<AcPackListItem>>('/ac-packs', {
      status: query.status,
      page: query.page,
      pageSize: query.pageSize,
    });
  }

  /** Single pack metadata/status surface — POLL this for generation status. */
  getPack(id: string): Observable<AcPack> {
    return this.api.get<AcPack>(`/ac-packs/${id}`);
  }

  /** The pack's analytics snapshot (restricted-filter applied per requester). */
  getPackAnalytics(id: string): Observable<AcPackAnalytics> {
    return this.api.get<AcPackAnalytics>(`/ac-packs/${id}/analytics`);
  }

  /**
   * Downloads a produced AC-pack artefact as a file. Bypasses `ApiService`
   * (which unwraps `{ data }` as JSON) and reads the raw blob plus headers so
   * the caller can trigger the browser download.
   */
  downloadPack(id: string, format: string): Observable<HttpResponse<Blob>> {
    return this.http.get(`${this.baseUrl}/ac-packs/${id}/download`, {
      params: { format },
      responseType: 'blob',
      observe: 'response',
      withCredentials: true,
    });
  }

  /** CIA: set/replace the supplementary narrative (PendingReview only). */
  updateCiaText(
    id: string,
    body: UpdateAcPackCiaTextRequest,
  ): Observable<AcPack> {
    return this.api.patch<AcPack>(`/ac-packs/${id}/cia-text`, body);
  }

  /** CIA: approve the pack (optional last-mile supplementary text edit). */
  approvePack(id: string, body: ApproveAcPackRequest = {}): Observable<AcPack> {
    return this.api.post<AcPack>(`/ac-packs/${id}/approve`, body);
  }

  /** CIA: distribute an approved pack to AC recipients. */
  distributePack(id: string): Observable<AcPackDistributionResult> {
    return this.api.post<AcPackDistributionResult>(
      `/ac-packs/${id}/distribute`,
    );
  }

  /** Distribution log for a pack. */
  packDistributions(
    id: string,
    page?: number,
    pageSize?: number,
  ): Observable<PagedResult<AcPackDistribution>> {
    return this.api.get<PagedResult<AcPackDistribution>>(
      `/ac-packs/${id}/distributions`,
      { page, pageSize },
    );
  }

  /* ---- AC dashboard ---- */

  /** Read-only live aggregates for AC members. */
  getDashboard(): Observable<AcDashboard> {
    return this.api.get<AcDashboard>('/ac-dashboard');
  }

  /* ---- AC action items ---- */

  createActionItem(
    body: CreateAcActionItemRequest,
  ): Observable<AcActionItem> {
    return this.api.post<AcActionItem>('/ac-action-items', body);
  }

  listActionItems(
    status?: string | null,
    page?: number,
    pageSize?: number,
  ): Observable<PagedResult<AcActionItem>> {
    return this.api.get<PagedResult<AcActionItem>>('/ac-action-items', {
      status,
      page,
      pageSize,
    });
  }

  /** CIA: mark in-progress and/or close (non-blank closureResponse closes). */
  updateActionItem(
    id: string,
    body: UpdateAcActionItemRequest,
  ): Observable<AcActionItem> {
    return this.api.patch<AcActionItem>(`/ac-action-items/${id}`, body);
  }

  /** ACChair: acknowledge a closed item (terminal). */
  acknowledgeActionItemClosure(id: string): Observable<AcActionItem> {
    return this.api.post<AcActionItem>(
      `/ac-action-items/${id}/acknowledge-closure`,
    );
  }

  /* ---- AC comments ---- */

  addComment(body: AddAcCommentRequest): Observable<AcComment> {
    return this.api.post<AcComment>('/ac-comments', body);
  }

  listComments(
    targetType: string,
    targetId: string,
  ): Observable<AcComment[]> {
    return this.api.get<AcComment[]>('/ac-comments', {
      targetType,
      targetId,
    });
  }

  /* ---- Finding visibility restriction (CIA) ---- */

  restrictFindingVisibility(
    type: string,
    id: string,
    body: RestrictFindingVisibilityRequest,
  ): Observable<FindingVisibilityRestriction> {
    return this.api.post<FindingVisibilityRestriction>(
      `/findings/${type}/${id}/restrict-visibility`,
      body,
    );
  }
}