import { Injectable, computed, inject, signal } from '@angular/core';

import { AuditsService } from './audits.service';
import { AuditListItem } from '../models';

/**
 * Shared, lazily-populated directory of audits keyed by id.
 *
 * Mirrors {@link UserLookupService} / {@link EntityLookupService}: the first call to {@link name} or
 * {@link options} kicks off a fire-and-forget, idempotent load of every audit (a single capped
 * 'load all' page). It never throws — on error the cache is left empty and callers fall back to the raw id.
 */
@Injectable({ providedIn: 'root' })
export class AuditLookupService {
  private readonly audits = inject(AuditsService);

  private readonly auditsById = signal<Map<string, AuditListItem>>(new Map());
  private loadStarted = false;

  /** Audits for populating a `<mat-select>`, ordered by name. */
  readonly options = computed<AuditListItem[]>(() =>
    [...this.auditsById().values()].sort((a, b) =>
      (a.name || '').localeCompare(b.name || ''),
    ),
  );

  /**
   * Resolves an audit id to its name.
   * Returns '—' for null/empty, the cached name when known, or the raw id
   * while unresolved / unknown. Triggers the lazy load on first use.
   */
  name(id: string | null | undefined): string {
    this.ensureLoaded();
    if (!id) {
      return '—';
    }
    return this.auditsById().get(id)?.name ?? id;
  }

  /** Idempotently starts the eager load of the audit directory. */
  ensureLoaded(): void {
    if (this.loadStarted) {
      return;
    }
    this.loadStarted = true;
    this.audits.list({ pageSize: 0 }).subscribe({
      next: (result) => {
        this.auditsById.update((prev) => {
          const next = new Map(prev);
          for (const a of result.items) {
            next.set(a.id, a);
          }
          return next;
        });
      },
      error: () => {
        // Non-fatal: leave the cache as-is; callers fall back to the raw id.
      },
    });
  }
}
