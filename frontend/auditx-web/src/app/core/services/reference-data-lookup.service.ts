import { Injectable, Signal, computed, inject, signal } from '@angular/core';

import { ReferenceDataService } from './reference-data.service';
import { ReferenceDataItem } from '../models';

/**
 * Shared, lazily-populated cache of ACTIVE reference-data items, keyed by
 * category then item code.
 *
 * Mirrors {@link UserLookupService}: the first read of a category via
 * {@link options} or {@link label} kicks off a fire-and-forget, idempotent load
 * of that category's active items. It never throws — on error the category is
 * left empty and callers fall back to the raw code. This means a component can
 * inject the service without forcing specs to flush the
 * `/reference-data/{category}` GET unless they actually exercise the lookup.
 */
@Injectable({ providedIn: 'root' })
export class ReferenceDataLookupService {
  private readonly service = inject(ReferenceDataService);

  /** category → (code → item). */
  private readonly itemsByCategory = signal<
    Map<string, Map<string, ReferenceDataItem>>
  >(new Map());
  private readonly loadStarted = new Set<string>();
  /** Memoised, category-specific option signals so `options()` is stable. */
  private readonly optionSignals = new Map<
    string,
    Signal<ReferenceDataItem[]>
  >();

  /**
   * Active items for populating a `<mat-select>`, ordered by sortOrder then
   * label. Loads the category on first read.
   */
  options(category: string): Signal<ReferenceDataItem[]> {
    let sig = this.optionSignals.get(category);
    if (!sig) {
      sig = computed(() =>
        [...(this.itemsByCategory().get(category)?.values() ?? [])].sort(
          (a, b) =>
            a.sortOrder - b.sortOrder ||
            (a.label || '').localeCompare(b.label || ''),
        ),
      );
      this.optionSignals.set(category, sig);
    }
    this.ensureLoaded(category);
    return sig;
  }

  /**
   * Resolves an item code to its human label within a category.
   * Returns '—' for null/empty, the cached label when known, or the raw code
   * while unresolved / unknown. Triggers the lazy load on first use.
   */
  label(category: string, code: string | null | undefined): string {
    this.ensureLoaded(category);
    if (!code) {
      return '—';
    }
    return this.itemsByCategory().get(category)?.get(code)?.label ?? code;
  }

  /**
   * Drops a category's cache so the next read reloads it. Call after an admin
   * mutation so dropdowns pick up the change without a full page refresh.
   */
  invalidate(category: string): void {
    if (!category) {
      return;
    }
    this.loadStarted.delete(category);
    this.itemsByCategory.update((prev) => {
      if (!prev.has(category)) {
        return prev;
      }
      const next = new Map(prev);
      next.delete(category);
      return next;
    });
  }

  /** Idempotently starts the eager load of a category's active items. */
  ensureLoaded(category: string): void {
    if (this.loadStarted.has(category)) {
      return;
    }
    this.loadStarted.add(category);
    this.service.list(category).subscribe({
      next: (items) => {
        this.itemsByCategory.update((prev) => {
          const next = new Map(prev);
          const byCode = new Map<string, ReferenceDataItem>();
          for (const item of items) {
            byCode.set(item.code, item);
          }
          next.set(category, byCode);
          return next;
        });
      },
      error: () => {
        // Non-fatal: leave the category empty; callers fall back to the raw code.
      },
    });
  }
}
