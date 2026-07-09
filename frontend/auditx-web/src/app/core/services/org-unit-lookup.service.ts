import { Injectable, computed, inject, signal } from '@angular/core';

import { OrgUnitService } from './org-unit.service';
import { OrgUnit } from '../models';
import { SelectOption } from '../../shared/components/searchable-select/searchable-select.component';

/**
 * Shared, lazily-populated cache of the organisation-unit tree, keyed by id.
 *
 * Mirrors {@link UserLookupService}: the first call to {@link options} or
 * {@link name} fires a one-shot load of the active org units. It is idempotent
 * and never throws — on error the cache is left empty and callers fall back to
 * the raw id, so injecting it never forces specs to flush the `/org-units` GET.
 *
 * Labels are rendered as the full hierarchy path (e.g. "Group / Retail /
 * Branch Ops") so a flat `<select>` still conveys where a unit sits.
 */
@Injectable({ providedIn: 'root' })
export class OrgUnitLookupService {
  private readonly orgUnits = inject(OrgUnitService);

  private readonly byId = signal<Map<string, OrgUnit>>(new Map());
  private loadStarted = false;

  /** Org units as `SelectOption`s, labelled by full path and ordered by path. */
  readonly options = computed<SelectOption[]>(() => {
    const map = this.byId();
    return [...map.values()]
      .map((u) => ({ value: u.id, label: this.pathFor(u.id, map) }))
      .sort((a, b) => a.label.localeCompare(b.label));
  });

  /**
   * Resolves an org-unit id to its full path.
   * Returns '—' for null/empty, the cached path when known, or the raw id while
   * unresolved / unknown. Triggers the lazy load on first use.
   */
  name(id: string | null | undefined): string {
    this.ensureLoaded();
    if (!id) {
      return '—';
    }
    const map = this.byId();
    return map.has(id) ? this.pathFor(id, map) : id;
  }

  /** Idempotently starts the eager load of the org-unit tree. */
  ensureLoaded(): void {
    if (this.loadStarted) {
      return;
    }
    this.loadStarted = true;
    this.orgUnits.list(false).subscribe({
      next: (units) => this.byId.set(new Map(units.map((u) => [u.id, u]))),
      error: () => {
        // Non-fatal: leave the cache empty; callers fall back to the raw id.
      },
    });
  }

  /** Forces a reload after a mutation (create/rename/reparent/archive). */
  reload(): void {
    this.loadStarted = false;
    this.ensureLoaded();
  }

  /** Builds "Root / … / Unit"; cycle-guarded against a malformed parent chain. */
  private pathFor(id: string, map: Map<string, OrgUnit>): string {
    const parts: string[] = [];
    const seen = new Set<string>();
    let current: OrgUnit | undefined = map.get(id);
    while (current && !seen.has(current.id)) {
      seen.add(current.id);
      parts.unshift(current.name);
      current = current.parentOrgUnitId ? map.get(current.parentOrgUnitId) : undefined;
    }
    return parts.join(' / ');
  }
}
