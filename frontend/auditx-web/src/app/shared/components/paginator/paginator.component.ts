import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
  output,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';

import { TranslatePipe } from '../../../core/i18n/translate.pipe';

/** Page-size sentinel meaning "load all" — the server caps it at {@link LOAD_ALL_CAP} rows. */
export const LOAD_ALL_PAGE_SIZE = 0;

/** Mirror of the backend `PageSpec.LoadAllCap` — the max rows a "load all" request returns. */
export const LOAD_ALL_CAP = 5000;

/**
 * Reusable offset paginator: first / prev / next / last navigation, a page-size dropdown (incl. "All"), and a
 * "showing X–Y of N" summary. Drive it from a {@link PagedResult}: bind `total`/`page`/`pageSize`, and re-query on
 * `pageChange` / `pageSizeChange`. Page-size `0` (= "All") is served as a single capped page — when the true total
 * exceeds the cap the summary shows a truncation note.
 */
@Component({
  selector: 'app-paginator',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatIconModule, MatSelectModule, TranslatePipe],
  template: `
    <div class="ax-pager">
      <div class="ax-pager__summary">
        @if (total() === 0) {
          {{ 'paginator.empty' | t }}
        } @else {
          {{ 'paginator.summary' | t: { start: rangeStart(), end: rangeEnd(), total: total() } }}
          @if (capped()) {
            <span class="ax-pager__capped">{{ 'paginator.capped' | t: { cap: cap } }}</span>
          }
        }
      </div>

      <div class="ax-pager__controls">
        <label class="ax-pager__size">
          <span class="ax-pager__size-label">{{ 'paginator.pageSize' | t }}</span>
          <mat-select
            [value]="pageSize()"
            [disabled]="disabled()"
            (valueChange)="pageSizeChange.emit($event)"
            [attr.aria-label]="'paginator.pageSize' | t"
          >
            @for (opt of pageSizeOptions(); track opt) {
              <mat-option [value]="opt">{{ opt }}</mat-option>
            }
            <mat-option [value]="0">{{ 'paginator.all' | t }}</mat-option>
          </mat-select>
        </label>

        <div class="ax-pager__nav">
          <span class="ax-pager__page">{{ 'paginator.page' | t: { page: page(), pages: totalPages() } }}</span>
          <button
            matIconButton
            type="button"
            [disabled]="!canPrev() || disabled()"
            (click)="pageChange.emit(1)"
            [attr.aria-label]="'paginator.first' | t"
          >
            <mat-icon>first_page</mat-icon>
          </button>
          <button
            matIconButton
            type="button"
            [disabled]="!canPrev() || disabled()"
            (click)="pageChange.emit(page() - 1)"
            [attr.aria-label]="'paginator.previous' | t"
          >
            <mat-icon>chevron_left</mat-icon>
          </button>
          <button
            matIconButton
            type="button"
            [disabled]="!canNext() || disabled()"
            (click)="pageChange.emit(page() + 1)"
            [attr.aria-label]="'paginator.next' | t"
          >
            <mat-icon>chevron_right</mat-icon>
          </button>
          <button
            matIconButton
            type="button"
            [disabled]="!canNext() || disabled()"
            (click)="pageChange.emit(totalPages())"
            [attr.aria-label]="'paginator.last' | t"
          >
            <mat-icon>last_page</mat-icon>
          </button>
        </div>
      </div>
    </div>
  `,
  styles: `
    .ax-pager {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      justify-content: space-between;
      gap: 0.75rem 1.25rem;
      padding: 0.5rem 0.25rem;
      font: var(--mat-sys-body-medium);
      color: var(--mat-sys-on-surface-variant);
    }
    .ax-pager__capped {
      margin-left: 0.4rem;
      color: var(--mat-sys-error);
      font: var(--mat-sys-body-small);
    }
    .ax-pager__controls {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 0.5rem 1rem;
    }
    .ax-pager__size {
      display: inline-flex;
      align-items: center;
      gap: 0.5rem;
    }
    .ax-pager__size-label {
      white-space: nowrap;
    }
    .ax-pager__size mat-select {
      width: 4.5rem;
    }
    .ax-pager__nav {
      display: inline-flex;
      align-items: center;
      gap: 0.1rem;
    }
    .ax-pager__page {
      margin-right: 0.4rem;
      white-space: nowrap;
      font-variant-numeric: tabular-nums;
    }
  `,
})
export class PaginatorComponent {
  /** Total matching rows (from the PagedResult). */
  readonly total = input.required<number>();
  /** Current 1-based page. */
  readonly page = input.required<number>();
  /** Current requested page size; `0` = "All". */
  readonly pageSize = input.required<number>();
  readonly disabled = input(false);
  readonly pageSizeOptions = input<number[]>([10, 25, 50, 100]);

  readonly pageChange = output<number>();
  readonly pageSizeChange = output<number>();

  protected readonly cap = LOAD_ALL_CAP;

  protected readonly isLoadAll = computed(() => this.pageSize() <= 0);

  protected readonly totalPages = computed(() => {
    const size = this.pageSize();
    return size <= 0 ? 1 : Math.max(1, Math.ceil(this.total() / size));
  });

  protected readonly rangeStart = computed(() =>
    this.total() === 0 ? 0 : this.isLoadAll() ? 1 : (this.page() - 1) * this.pageSize() + 1,
  );

  protected readonly rangeEnd = computed(() =>
    this.isLoadAll()
      ? Math.min(this.total(), LOAD_ALL_CAP)
      : Math.min(this.page() * this.pageSize(), this.total()),
  );

  /** "Load all" truncated the result (more rows exist than the cap returns). */
  protected readonly capped = computed(() => this.isLoadAll() && this.total() > LOAD_ALL_CAP);

  protected readonly canPrev = computed(() => !this.isLoadAll() && this.page() > 1);
  protected readonly canNext = computed(() => !this.isLoadAll() && this.page() < this.totalPages());
}
