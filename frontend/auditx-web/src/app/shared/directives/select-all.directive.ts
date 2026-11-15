import { Directive, inject, OnDestroy } from '@angular/core';
import { MatSelect, MatSelectChange } from '@angular/material/select';
import { Subscription } from 'rxjs';

import { TranslationService } from '../../core/i18n/translation.service';

/**
 * Adds a "Select all / Clear" header to the top of every multi-select panel. Because the selector targets
 * <code>mat-select[multiple]</code>, importing this directive into a component wires up all of that component's
 * multi-selects at once — no per-field markup. The header is injected into the CDK overlay panel (which lives
 * outside component style scope), so its styling lives in the global stylesheet under <code>.ax-select-all</code>.
 */
@Directive({
  selector: 'mat-select[multiple]',
  standalone: true,
})
export class SelectAllDirective implements OnDestroy {
  private readonly select = inject(MatSelect);
  private readonly i18n = inject(TranslationService);
  private readonly sub: Subscription;

  constructor() {
    this.sub = this.select.openedChange.subscribe((open) => {
      if (open) {
        // Defer so the overlay panel is in the DOM before we insert the header.
        setTimeout(() => this.renderHeader(), 0);
      }
    });
  }

  private renderHeader(): void {
    const panel = this.select.panel?.nativeElement as HTMLElement | undefined;
    if (!panel || panel.querySelector('.ax-select-all')) {
      return;
    }
    const bar = document.createElement('div');
    bar.className = 'ax-select-all';
    bar.appendChild(this.button(this.i18n.translate('common.selectAll'), () => this.setAll(true)));
    bar.appendChild(this.button(this.i18n.translate('common.clearAll'), () => this.setAll(false)));
    // Join the panel's search header when there is one (both pinned to the top would overlap).
    const head = panel.querySelector(':scope > .ax-select-head');
    if (head) {
      head.appendChild(bar);
    } else {
      panel.insertBefore(bar, panel.firstChild);
    }
  }

  private button(label: string, onClick: () => void): HTMLButtonElement {
    const btn = document.createElement('button');
    btn.type = 'button';
    btn.className = 'ax-select-all__btn';
    btn.textContent = label;
    // Keep the panel open / focused when the header button is pressed.
    btn.addEventListener('mousedown', (e) => e.preventDefault());
    btn.addEventListener('click', (e) => {
      e.preventDefault();
      e.stopPropagation();
      onClick();
    });
    return btn;
  }

  /**
   * Select all / Clear act on the options in view: with a search typed, only the matches are added or removed and
   * the rest of the selection is kept; with no search, every option.
   */
  private setAll(select: boolean): void {
    const shown = this.select.options.filter((o) => o._getHostElement().style.display !== 'none');
    const filtered = shown.length < this.select.options.length;
    const current: unknown[] = Array.isArray(this.select.value) ? this.select.value : [];
    const shownValues = new Set(shown.map((o) => o.value));
    const values = select
      ? [...new Set([...(filtered ? current : []), ...shown.filter((o) => !o.disabled).map((o) => o.value)])]
      : filtered
        ? current.filter((v) => !shownValues.has(v))
        : [];
    const control = this.select.ngControl?.control;
    if (control) {
      control.setValue(values);
      control.markAsDirty();
    } else {
      this.select.value = values;
    }
    // Mirror a real selection so `(selectionChange)` listeners also react.
    this.select.selectionChange.emit(new MatSelectChange(this.select, values));
  }

  ngOnDestroy(): void {
    this.sub.unsubscribe();
  }
}
