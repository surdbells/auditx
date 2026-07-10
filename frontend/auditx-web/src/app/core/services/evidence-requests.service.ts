import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { ApiService } from './api.service';
import { EvidenceRequest, RequestEvidenceRequest } from '../models';

/**
 * Typed client for the P2-D evidence-request endpoints. Evidence requests are a separate aggregate — mutating
 * one does NOT change the audit version. Mutate=RespondItem, read=ViewAudit. The receive/waive/delete endpoints
 * are NOT nested under /audits.
 */
@Injectable({ providedIn: 'root' })
export class EvidenceRequestsService {
  private readonly api = inject(ApiService);

  list(auditId: string): Observable<EvidenceRequest[]> {
    return this.api.get<EvidenceRequest[]>(`/audits/${auditId}/evidence-requests`);
  }

  request(auditId: string, body: RequestEvidenceRequest): Observable<EvidenceRequest> {
    return this.api.post<EvidenceRequest>(`/audits/${auditId}/evidence-requests`, body);
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
