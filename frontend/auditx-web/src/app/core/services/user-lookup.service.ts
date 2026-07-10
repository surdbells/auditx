import { Injectable, computed, inject, signal } from '@angular/core';

import { UsersService } from './users.service';
import { UserDirectoryEntry } from '../models';

/**
 * Shared, lazily-populated directory of users keyed by id.
 *
 * The first call to {@link displayName} or {@link options} kicks off a
 * fire-and-forget load of every user (a single capped 'load all' page). It is
 * idempotent and never throws: on error the cache is simply left empty, so
 * consumers fall back to the raw id. This means a component can inject the
 * service without forcing specs to flush the `/users` GET unless they actually
 * exercise the lookup.
 */
@Injectable({ providedIn: 'root' })
export class UserLookupService {
  private readonly users = inject(UsersService);

  private readonly usersById = signal<Map<string, UserDirectoryEntry>>(new Map());
  private loadStarted = false;

  /** Users for populating a `<mat-select>`, ordered by display name. */
  readonly options = computed<UserDirectoryEntry[]>(() =>
    [...this.usersById().values()].sort((a, b) =>
      (a.displayName || '').localeCompare(b.displayName || ''),
    ),
  );

  /**
   * Resolves a user id to a human-readable name.
   * Returns '—' for null/empty, the cached display name when known, or the
   * raw id while unresolved / unknown. Triggers the lazy load on first use.
   */
  displayName(id: string | null | undefined): string {
    this.ensureLoaded();
    if (!id) {
      return '—';
    }
    return this.usersById().get(id)?.displayName ?? id;
  }

  /** Idempotently starts the eager load of the user directory. */
  ensureLoaded(): void {
    if (this.loadStarted) {
      return;
    }
    this.loadStarted = true;
    this.users.directory({ pageSize: 0 }).subscribe({
      next: (page) => {
        this.usersById.update((prev) => {
          const next = new Map(prev);
          for (const u of page.items) {
            next.set(u.id, u);
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
