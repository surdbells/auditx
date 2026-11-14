import { Injectable, Signal, computed, effect, inject, signal } from '@angular/core';

import { AuthService } from './auth.service';
import { ReferenceDataService } from './reference-data.service';
import { ReferenceDataItem } from '../models';

/**
 * Shared, lazily-populated cache of reference-data items, keyed by category
 * then item code.
 *
 * The cache holds ALL items (active + inactive) so {@link label} can resolve a
 * historical code even after its item is deactivated — otherwise a deactivated
 * audit-type / entity-type would render as its raw code in existing rows.
 * {@link options} (form/filter dropdowns) exposes only the active items.
 *
 * Mirrors {@link UserLookupService}: the first read of a category via
 * {@link options} or {@link label} kicks off a fire-and-forget, idempotent load.
 * It never throws — on error the category is left empty and callers fall back to
 * the raw code. This means a component can inject the service without forcing
 * specs to flush the `/reference-data/{category}` GET unless they actually
 * exercise the lookup.
 */
/** The label `item` carried at `asOf`: the last version that had taken effect by then, else the current label. */
export function labelAsOf(item: ReferenceDataItem, asOf: string | Date): string {
  const history = item.labelHistory ?? [];
  if (history.length === 0) {
    return item.label;
  }
  const at = (asOf instanceof Date ? asOf : new Date(asOf)).getTime();
  if (Number.isNaN(at)) {
    return item.label;
  }
  let label = history[0].label;
  for (const version of history) {
    if (new Date(version.effectiveFrom).getTime() <= at) {
      label = version.label;
    }
  }
  return label;
}

@Injectable({ providedIn: 'root' })
export class ReferenceDataLookupService {
  private readonly service = inject(ReferenceDataService);

  /**
   * How long a loaded category is trusted. Another administrator may change a list at any time; after this the next
   * read quietly reloads it (the current values stay on screen meanwhile), so changes show up without a page refresh
   * or a fresh sign-in.
   */
  static readonly StaleAfterMs = 60_000;
  private readonly loadedAt = new Map<string, number>();

  constructor() {
    // A different person signing in on this tab starts from a clean cache.
    const auth = inject(AuthService);
    let lastUser: string | null | undefined;
    effect(() => {
      const user = auth.session()?.userId ?? null;
      if (lastUser !== undefined && user !== lastUser) {
        this.reset();
      }
      lastUser = user;
    });
  }

  /** Forgets every cached list. */
  reset(): void {
    this.loadStarted.clear();
    this.loadedAt.clear();
    this.itemsByCategory.set(new Map());
  }

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
   * ACTIVE items for populating a `<mat-select>`, ordered by sortOrder then
   * label. Loads the category on first read. Inactive items are cached (so
   * {@link label} can still resolve historical codes) but never offered here.
   */
  options(category: string): Signal<ReferenceDataItem[]> {
    let sig = this.optionSignals.get(category);
    if (!sig) {
      sig = computed(() =>
        [...(this.itemsByCategory().get(category)?.values() ?? [])]
          .filter((i) => i.isActive)
          .sort(
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
   *
   * Pass `asOf` — the date the record was created — to get the label as it read then: renaming a value applies
   * from the rename onwards and never rewrites what older records said. Without `asOf` the current label is used
   * (pickers, filters, anything that is about "now").
   */
  label(category: string, code: string | null | undefined, asOf?: string | Date | null): string {
    this.ensureLoaded(category);
    if (!code) {
      return '—';
    }
    const item = this.itemsByCategory().get(category)?.get(code);
    if (!item) {
      return code;
    }
    return asOf ? labelAsOf(item, asOf) : item.label;
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
    this.loadedAt.delete(category);
    this.itemsByCategory.update((prev) => {
      if (!prev.has(category)) {
        return prev;
      }
      const next = new Map(prev);
      next.delete(category);
      return next;
    });
  }

  /** Idempotently starts the eager load of a category's items (active + inactive, for label resolution). */
  ensureLoaded(category: string): void {
    if (this.loadStarted.has(category)) {
      const loaded = this.loadedAt.get(category);
      if (loaded === undefined || Date.now() - loaded < ReferenceDataLookupService.StaleAfterMs) {
        return; // still loading, or fresh enough
      }
    }
    this.loadStarted.add(category);
    // Marked now so a burst of reads during the reload does not start more of them.
    this.loadedAt.set(category, Date.now());
    this.service.list(category, true).subscribe({
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
