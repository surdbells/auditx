import { Injectable, computed, inject, signal } from '@angular/core';

import { UniverseService } from './universe.service';
import { CursorPage, EntityListItem } from '../models';

/** Max entities pulled per page while eagerly caching the universe. */
const PAGE_LIMIT = 200;
/** Safety cap so a runaway cursor never loops forever. */
const MAX_PAGES = 25;

/**
 * Shared, lazily-populated directory of auditable entities keyed by id.
 *
 * Mirrors {@link UserLookupService}: the first call to {@link name} or
 * {@link options} kicks off a fire-and-forget, idempotent load of every
 * entity. It never throws — on error the cache is left empty and callers fall
 * back to the raw id.
 */
@Injectable({ providedIn: 'root' })
export class EntityLookupService {
  private readonly universe = inject(UniverseService);

  private readonly entitiesById = signal<Map<string, EntityListItem>>(new Map());
  private loadStarted = false;

  /** Entities for populating a `<mat-select>`, ordered by name. */
  readonly options = computed<EntityListItem[]>(() =>
    [...this.entitiesById().values()].sort((a, b) =>
      (a.name || '').localeCompare(b.name || ''),
    ),
  );

  /**
   * Resolves an entity id to its name.
   * Returns '—' for null/empty, the cached name when known, or the raw id
   * while unresolved / unknown. Triggers the lazy load on first use.
   */
  name(id: string | null | undefined): string {
    this.ensureLoaded();
    if (!id) {
      return '—';
    }
    return this.entitiesById().get(id)?.name ?? id;
  }

  /** Idempotently starts the eager load of the entity directory. */
  ensureLoaded(): void {
    if (this.loadStarted) {
      return;
    }
    this.loadStarted = true;
    this.loadPage(null, 0);
  }

  private loadPage(cursor: string | null, pageIndex: number): void {
    if (pageIndex >= MAX_PAGES) {
      return;
    }
    this.universe.list({ limit: PAGE_LIMIT, cursor }).subscribe({
      next: (page: CursorPage<EntityListItem>) => {
        this.entitiesById.update((prev) => {
          const next = new Map(prev);
          for (const e of page.items) {
            next.set(e.id, e);
          }
          return next;
        });
        if (page.hasMore && page.nextCursor) {
          this.loadPage(page.nextCursor, pageIndex + 1);
        }
      },
      error: () => {
        // Non-fatal: leave the cache as-is; callers fall back to the raw id.
      },
    });
  }
}
