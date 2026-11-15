import { DestroyRef, Directive, Renderer2, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatOption } from '@angular/material/core';
import { MatSelect } from '@angular/material/select';
import { Subscription } from 'rxjs';

import { TranslationService } from '../../core/i18n/translation.service';

/** The parts of MatSelect's keyboard handling this directive narrows while a search is active. */
interface KeyManagerLike {
  skipPredicate(predicate: (item: MatOption) => boolean): unknown;
  setFirstItemActive(): void;
  activeItem: MatOption | null;
}

const SEARCH_ICON =
  '<svg viewBox="0 0 24 24" width="18" height="18" aria-hidden="true" focusable="false">' +
  '<circle cx="11" cy="11" r="7" fill="none" stroke="currentColor" stroke-width="2"/>' +
  '<path d="m20 20-3.5-3.5" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round"/></svg>';

/** Keys the search box hands on to the list: moving through it, choosing, and closing it. */
const LIST_KEYS = new Set(['ArrowDown', 'ArrowUp', 'Enter', 'Escape', 'Tab', 'PageUp', 'PageDown']);

/**
 * Makes every `<mat-select>` searchable: when its list opens, a search box sits at the top of the panel; typing
 * narrows the options to those whose text contains the words typed (the selected option always stays), the first
 * match is highlighted so Enter picks it, and the arrow keys move through the matches only. Works for single and
 * multiple selects and option groups, with no change to the select's own markup or bindings. Opt out with
 * `axNoSearch` on a select where a search makes no sense.
 */
@Directive({
  selector: 'mat-select:not([axNoSearch])',
})
export class SelectSearchDirective {
  private readonly select = inject(MatSelect, { self: true });
  private readonly renderer = inject(Renderer2);
  private readonly i18n = inject(TranslationService);

  private box: HTMLElement | null = null;
  private input: HTMLInputElement | null = null;
  private empty: HTMLElement | null = null;
  private query = '';
  private readonly hidden = new Set<MatOption>();
  private optionChanges: Subscription | null = null;
  private readonly cleanups: (() => void)[] = [];

  constructor() {
    // The panel is rendered into the overlay on the next tick, so attach once it is there.
    this.select.openedChange.pipe(takeUntilDestroyed()).subscribe((open) => (open ? setTimeout(() => this.attach(), 0) : this.detach()));
    inject(DestroyRef).onDestroy(() => this.detach());
  }

  private get keyManager(): KeyManagerLike | null {
    return (this.select as unknown as { _keyManager?: KeyManagerLike })._keyManager ?? null;
  }

  private attach(): void {
    const panel = this.select.panel?.nativeElement as HTMLElement | undefined;
    if (!this.select.panelOpen || !panel || panel.querySelector(':scope > .ax-select-head')) {
      return;
    }

    this.query = '';
    this.showAll();

    const label = this.i18n.translate('common.search');
    // One sticky header for the panel: the search row, and (on a multi-select) the Select all / Clear bar under it.
    const box = this.renderer.createElement('div') as HTMLElement;
    this.renderer.addClass(box, 'ax-select-head');
    const row = this.renderer.createElement('div') as HTMLElement;
    this.renderer.addClass(row, 'ax-select-search');
    row.innerHTML = SEARCH_ICON;
    this.renderer.appendChild(box, row);
    const input = this.renderer.createElement('input') as HTMLInputElement;
    this.renderer.addClass(input, 'ax-select-search__input');
    this.renderer.setAttribute(input, 'type', 'text');
    this.renderer.setAttribute(input, 'autocomplete', 'off');
    this.renderer.setAttribute(input, 'placeholder', label);
    this.renderer.setAttribute(input, 'aria-label', label);
    this.renderer.appendChild(row, input);

    const empty = this.renderer.createElement('p') as HTMLElement;
    this.renderer.addClass(empty, 'ax-select-search__empty');
    this.renderer.setProperty(empty, 'textContent', this.i18n.translate('common.noResults'));
    this.renderer.setAttribute(empty, 'hidden', '');

    // A multi-select's Select all / Clear bar, if already there, joins this header rather than pinning on its own.
    const selectAll = panel.querySelector(':scope > .ax-select-all');
    if (selectAll) {
      this.renderer.appendChild(box, selectAll);
    }
    this.renderer.insertBefore(panel, box, panel.firstChild);
    this.renderer.insertBefore(panel, empty, box.nextSibling);
    this.box = box;
    this.input = input;
    this.empty = empty;

    this.cleanups.push(
      this.renderer.listen(input, 'input', () => this.filter(input.value)),
      // Typing goes to the search box, not to the list's type-ahead (which would jump to or pick an option).
      this.renderer.listen(input, 'keydown', (event: KeyboardEvent) => {
        if (!LIST_KEYS.has(event.key)) {
          event.stopPropagation();
        }
      }),
      this.renderer.listen(box, 'click', (event: Event) => event.stopPropagation()),
    );

    // Hidden options are skipped by the arrow keys; disabled ones stay reachable while open, as Material intends.
    this.keyManager?.skipPredicate((option) => this.hidden.has(option));
    this.optionChanges = this.select.options.changes.subscribe(() => this.filter(this.query));

    queueMicrotask(() => input.focus({ preventScroll: true }));
  }

  private filter(raw: string): void {
    this.query = raw;
    const q = raw.trim().toLowerCase();
    this.hidden.clear();

    for (const option of this.select.options) {
      const match = !q || option.selected || option.viewValue.toLowerCase().includes(q);
      option._getHostElement().style.display = match ? '' : 'none';
      if (!match) {
        this.hidden.add(option);
      }
    }

    // A group whose options are all filtered out disappears with them.
    const panel = this.select.panel?.nativeElement as HTMLElement | undefined;
    panel?.querySelectorAll<HTMLElement>('mat-optgroup').forEach((group) => {
      const anyVisible = [...group.querySelectorAll<HTMLElement>('mat-option')].some((o) => o.style.display !== 'none');
      group.style.display = anyVisible ? '' : 'none';
    });

    const visible = this.select.options.length - this.hidden.size;
    if (this.empty) {
      this.empty.hidden = visible > 0;
    }

    // The first match is highlighted, so Enter picks it.
    const manager = this.keyManager;
    if (q && manager && (!manager.activeItem || this.hidden.has(manager.activeItem))) {
      manager.setFirstItemActive();
    }
  }

  private showAll(): void {
    for (const option of this.select.options ?? []) {
      option._getHostElement().style.display = '';
    }
    this.hidden.clear();
  }

  private detach(): void {
    this.optionChanges?.unsubscribe();
    this.optionChanges = null;
    while (this.cleanups.length) {
      this.cleanups.pop()!();
    }
    // Restore Material's own rule for the closed list.
    const select = this.select as unknown as { _skipPredicate?: (o: MatOption) => boolean };
    if (select._skipPredicate) {
      this.keyManager?.skipPredicate(select._skipPredicate);
    }
    // Options keep their element between openings, so undo the filtering before the next one.
    this.showAll();
    this.box?.remove();
    this.empty?.remove();
    this.box = this.input = this.empty = null;
    this.query = '';
  }
}
