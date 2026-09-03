import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';

import { ApiService } from './api.service';
import { environment } from '../../../environments/environment';
import { ApiResponse, EvidenceRequest, RequestEvidenceRequest } from '../models';

/**
 * Typed client for the P2-D evidence-request endpoints. Evidence requests are a separate aggregate — mutating
 * one does NOT change the audit version. Mutate=RespondItem, read=ViewAudit. The receive/waive/delete endpoints
 * are NOT nested under /audits.
 */
@Injectable({ providedIn: 'root' })
export class EvidenceRequestsService {
  private readonly api = inject(ApiService);
  private readonly http = inject(HttpClient);
  private readonly baseUrl = environment.apiBaseUrl;

  list(auditId: string): Observable<EvidenceRequest[]> {
    return this.api.get<EvidenceRequest[]>(`/audits/${auditId}/evidence-requests`);
  }

  /** The current user's (auditee's) own document requests — their upload worklist. */
  mine(outstandingOnly = false): Observable<EvidenceRequest[]> {
    return this.api.get<EvidenceRequest[]>('/my/evidence-requests', { outstandingOnly });
  }

  request(auditId: string, body: RequestEvidenceRequest): Observable<EvidenceRequest> {
    return this.api.post<EvidenceRequest>(`/audits/${auditId}/evidence-requests`, body);
  }

  /** The auditee uploads a document against a request (multipart). Returns the updated request with its files. */
  uploadFile(id: string, file: File): Observable<EvidenceRequest> {
    const form = new FormData();
    form.append('file', file);
    return this.http
      .post<ApiResponse<EvidenceRequest>>(`${this.baseUrl}/evidence-requests/${id}/files`, form, { withCredentials: true })
      .pipe(map((r) => r.data));
  }

  markReceived(id: string, version: string): Observable<EvidenceRequest> {
    return this.api.post<EvidenceRequest>(`/evidence-requests/${id}/received`, { version });
  }

  waive(id: string, reason: string, version: string): Observable<EvidenceRequest> {
    return this.api.post<EvidenceRequest>(`/evidence-requests/${id}/waive`, { reason, version });
  }

  delete(id: string, version: string): Observable<void> {
    return this.api.deleteVoid(`/evidence-requests/${id}?version=${encodeURIComponent(version)}`);
  }
}
