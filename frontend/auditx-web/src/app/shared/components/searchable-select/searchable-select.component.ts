import {
  ChangeDetectionStrategy,
  Component,
  ViewEncapsulation,
  computed,
  forwardRef,
  inject,
  input,
  signal,
  viewChild,
  type ElementRef,
} from '@angular/core';
import {
  ControlValueAccessor,
  NG_VALUE_ACCESSOR,
} from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatIconModule } from '@angular/material/icon';

import { TranslationService } from '../../../core/i18n/translation.service';

/** A selectable option for {@link SearchableSelectComponent}. */
export interface SelectOption {
  value: string;
  label: string;
  disabled?: boolean;
}

/**
 * A drop-in, dependency-free searchable single-select. Wraps `mat-form-field` + `mat-select` and adds a
 * sticky type-to-filter search box inside the panel, so any long dropdown (users, entities, audit types…)
 * becomes filterable. Implements ControlValueAccessor, so it slots into reactive forms with
 * `formControlName` exactly like a native `<mat-select>`.
 *
 * The panel is rendered in a CDK overlay outside the component view, so styles are global (ViewEncapsulation
 * None) but namespaced with the `ss-` prefix + the `ss-panel` panel class.
 */
@Component({
  selector: 'app-searchable-select',
  changeDetection: ChangeDetectionStrategy.OnPush,
  encapsulation: ViewEncapsulation.None,
  imports: [MatFormFieldModule, MatSelectModule, MatIconModule],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => SearchableSelectComponent),
      multi: true,
    },
  ],
  template: `
    <mat-form-field appearance="outline" class="ss-field">
      @if (label()) {
        <mat-label>{{ label() }}</mat-label>
      }
      <mat-select
        [value]="value()"
        [disabled]="disabled()"
        [placeholder]="placeholder()"
        panelClass="ss-panel"
        (selectionChange)="select($event.value)"
        (openedChange)="onOpened($event)"
      >
        <div class="ss-search" (click)="$event.stopPropagation()">
          <mat-icon class="ss-search__icon" aria-hidden="true">search</mat-icon>
          <input
            #box
            class="ss-search__input"
            type="text"
            autocomplete="off"
            [attr.aria-label]="searchLabel()"
            [placeholder]="searchLabel()"
            [value]="query()"
            (input)="query.set(box.value)"
            (keydown)="onSearchKeydown($event)"
            (click)="$event.stopPropagation()"
          />
        </div>

        @if (clearable()) {
          <mat-option [value]="null">{{ clearLabel() }}</mat-option>
        }
        @for (opt of filtered(); track opt.value) {
          <mat-option [value]="opt.value" [disabled]="opt.disabled ?? false">{{ opt.label }}</mat-option>
        }
        @if (!filtered().length) {
          <p class="ss-empty">{{ noResultsLabel() }}</p>
        }
      </mat-select>
    </mat-form-field>
  `,
  styles: `
    app-searchable-select { display: block; }
    .ss-field { width: 100%; }

    .ss-panel .ss-search {
      position: sticky;
      top: 0;
      z-index: 2;
      display: flex;
      align-items: center;
      gap: 0.4rem;
      padding: 0.35rem 0.6rem;
      margin: -8px 0 4px; /* pull over the panel's default top padding */
      background: var(--mat-sys-surface-container, #fff);
      border-bottom: 1px solid var(--mat-sys-outline-variant);
    }
    .ss-panel .ss-search__icon {
      flex: 0 0 auto;
      font-size: 18px;
      width: 18px;
      height: 18px;
      color: var(--mat-sys-on-surface-variant);
    }
    .ss-panel .ss-search__input {
      flex: 1 1 auto;
      min-width: 0;
      border: none;
      outline: none;
      background: transparent;
      font: inherit;
      font-size: 0.9rem;
      color: var(--mat-sys-on-surface);
      padding: 0.2rem 0;
    }
    .ss-panel .ss-empty {
      margin: 0;
      padding: 0.6rem 1rem;
      color: var(--mat-sys-on-surface-variant);
      font-size: 0.85rem;
    }
  `,
})
export class SearchableSelectComponent implements ControlValueAccessor {
  private readonly i18n = inject(TranslationService);

  readonly options = input<SelectOption[]>([]);
  readonly label = input<string>('');
  readonly placeholder = input<string>('');
  /** Search-box placeholder; defaults to the shared "Search…" translation. */
  readonly searchPlaceholder = input<string>('');
  /** Empty-state text; defaults to the shared "No matches" translation. */
  readonly noResults = input<string>('');
  /** When true, an explicit "clear" option (value null) is shown at the top. */
  readonly clearable = input<boolean>(false);
  readonly clearLabel = input<string>('');

  private readonly box = viewChild<ElementRef<HTMLInputElement>>('box');

  readonly value = signal<string | null>(null);
  readonly disabled = signal(false);
  readonly query = signal('');

  /** Options filtered by the query; the selected option is always kept so the trigger keeps its label. */
  readonly filtered = computed<SelectOption[]>(() => {
    const q = this.query().trim().toLowerCase();
    const opts = this.options();
    if (!q) {
      return opts;
    }
    const selected = this.value();
    return opts.filter((o) => o.label.toLowerCase().includes(q) || o.value === selected);
  });

  readonly searchLabel = computed(() => this.searchPlaceholder() || this.i18n.translate('common.search'));
  readonly noResultsLabel = computed(() => this.noResults() || this.i18n.translate('common.noResults'));

  private onChange: (value: string | null) => void = () => undefined;
  private onTouched: () => void = () => undefined;

  writeValue(value: string | null): void {
    this.value.set(value ?? null);
  }
  registerOnChange(fn: (value: string | null) => void): void {
    this.onChange = fn;
  }
  registerOnTouched(fn: () => void): void {
    this.onTouched = fn;
  }
  setDisabledState(isDisabled: boolean): void {
    this.disabled.set(isDisabled);
  }

  select(value: string | null): void {
    this.value.set(value);
    this.onChange(value);
    this.onTouched();
  }

  onOpened(opened: boolean): void {
    if (opened) {
      // Focus the search box once the panel is in the DOM.
      queueMicrotask(() => this.box()?.nativeElement.focus());
    } else {
      this.query.set('');
    }
  }

  /** Keep keystrokes in the search box from reaching mat-select's typeahead (which would jump/select options). */
  onSearchKeydown(event: KeyboardEvent): void {
    if (event.key !== 'ArrowDown' && event.key !== 'ArrowUp' && event.key !== 'Enter' && event.key !== 'Escape') {
      event.stopPropagation();
    }
  }
}
