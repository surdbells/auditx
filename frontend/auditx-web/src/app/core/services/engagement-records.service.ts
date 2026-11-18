import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { ApiService } from './api.service';
import { EngagementRecords, MeetingKind, RecordMeetingRequest } from '../models';

/**
 * An audit's engagement records: the engagement letter, the team's independence declarations and the kickoff and exit
 * meeting records. Every change answers with the records as they now stand for the signed-in user. Declaring and
 * accepting a conflict carry (and move) the AUDIT's version; the letter and the meetings carry their own.
 */
@Injectable({ providedIn: 'root' })
export class EngagementRecordsService {
  private readonly api = inject(ApiService);

  get(auditId: string): Observable<EngagementRecords> {
    return this.api.get<EngagementRecords>(`/audits/${auditId}/engagement`);
  }

  /** Write the first draft (no version) or change the letter (its version). */
  saveLetter(auditId: string, body: string, letterVersion: string | null): Observable<EngagementRecords> {
    return this.api.put<EngagementRecords>(`/audits/${auditId}/engagement/letter`, { body, letterVersion });
  }

  issueLetter(auditId: string, letterVersion: string): Observable<EngagementRecords> {
    return this.api.post<EngagementRecords>(`/audits/${auditId}/engagement/letter/issue`, { letterVersion });
  }

  /** The auditee's acknowledgement — addressed by the letter, since the audit's own routes are closed to its auditee. */
  acknowledgeLetter(letterId: string, comment: string | null, letterVersion: string): Observable<EngagementRecords> {
    return this.api.post<EngagementRecords>(`/engagement-letters/${letterId}/acknowledge`, { comment, letterVersion });
  }

  declareIndependence(auditId: string, hasConflict: boolean, conflictDetails: string | null, version: string): Observable<EngagementRecords> {
    return this.api.post<EngagementRecords>(`/audits/${auditId}/engagement/independence`, { hasConflict, conflictDetails, version });
  }

  acceptConflict(auditId: string, declarationId: string, safeguards: string, version: string): Observable<EngagementRecords> {
    return this.api.post<EngagementRecords>(`/audits/${auditId}/engagement/independence/${declarationId}/accept`, { safeguards, version });
  }

  recordMeeting(auditId: string, kind: MeetingKind, request: RecordMeetingRequest): Observable<EngagementRecords> {
    return this.api.put<EngagementRecords>(`/audits/${auditId}/engagement/meetings/${kind}`, request);
  }
}
