import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { ApiService } from './api.service';
import { AuditProcedure, RecordProcedureRequest } from '../models';

/**
 * Typed client for the P2-C execution-procedure endpoints. Procedures are a separate aggregate
 * (like time entries) — recording/deleting does NOT change the audit's rowversion. Record=RespondItem,
 * read=ViewAudit. The delete endpoint is NOT nested under /audits.
 */
@Injectable({ providedIn: 'root' })
export class ProceduresService {
  private readonly api = inject(ApiService);

  list(auditId: string): Observable<AuditProcedure[]> {
    return this.api.get<AuditProcedure[]>(`/audits/${auditId}/procedures`);
  }

  record(auditId: string, body: RecordProcedureRequest): Observable<AuditProcedure> {
    return this.api.post<AuditProcedure>(`/audits/${auditId}/procedures`, body);
  }

  delete(id: string, version: string): Observable<void> {
    return this.api.deleteVoid(`/procedures/${id}?version=${encodeURIComponent(version)}`);
  }
}
