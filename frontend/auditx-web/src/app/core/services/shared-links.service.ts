import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

import { ApiService } from './api.service';
import { SharedLink, SharedLinkTarget } from '../models';

/**
 * Shareable report links (D3-B). A link is a revocable, optionally-expiring reference — resolving it returns only
 * the target descriptor, and opening the report still passes ViewReport + the report's own resource scope.
 */
@Injectable({ providedIn: 'root' })
export class SharedLinksService {
  private readonly api = inject(ApiService);

  /** Create a shareable link to a report (optional expiry in days). */
  createForReport(reportId: string, expiresInDays: number | null): Observable<SharedLink> {
    return this.api.post<SharedLink>(`/reports/${reportId}/share`, { expiresInDays });
  }

  /** Existing links for a report (newest first, includes revoked/expired for audit). */
  listForReport(reportId: string): Observable<SharedLink[]> {
    return this.api.get<SharedLink[]>(`/reports/${reportId}/share`);
  }

  /** Resolve a slug to its target descriptor (404 if missing/revoked/expired, 403 if not permitted). */
  resolve(slug: string): Observable<SharedLinkTarget> {
    return this.api.get<SharedLinkTarget>(`/shared-links/${slug}`);
  }

  /** Revoke a link (creator only). */
  revoke(id: string, version: string): Observable<void> {
    return this.api.deleteVoid(`/shared-links/${id}?version=${encodeURIComponent(version)}`);
  }
}
