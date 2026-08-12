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
    panel.insertBefore(bar, panel.firstChild);
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

  private setAll(select: boolean): void {
    const values = select
      ? this.select.options.filter((o) => !o.disabled).map((o) => o.value)
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
