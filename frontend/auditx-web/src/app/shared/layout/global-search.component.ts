import {
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  HostListener,
  computed,
  inject,
  signal,
} from '@angular/core';
import { ReactiveFormsModule, FormControl } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { IconComponent } from '../../core/icons/icon.component';
import { debounceTime, distinctUntilChanged, map, switchMap, of, catchError } from 'rxjs';

import { SearchService } from '../../core/services/search.service';
import { SearchHit } from '../../core/models';
import { TranslatePipe } from '../../core/i18n/translate.pipe';

/** Fixed group order + per-type display config. Keys mirror the backend hit `type`. */
const HIT_CONFIG: Record<string, { labelKey: string; icon: string; path: string }> = {
  audit: { labelKey: 'search.group.audits', icon: 'fact_check', path: '/audits' },
  plan: { labelKey: 'search.group.plans', icon: 'event_note', path: '/planning' },
  exception: { labelKey: 'search.group.exceptions', icon: 'report_problem', path: '/exceptions' },
  template: { labelKey: 'search.group.templates', icon: 'description', path: '/admin/templates' },
  user: { labelKey: 'search.group.users', icon: 'person', path: '/admin/users' },
};
const HIT_ORDER = ['audit', 'plan', 'exception', 'template', 'user'];
const MIN_TERM = 2;

interface HitVm extends SearchHit {
  index: number;
  icon: string;
  path: string;
}
interface GroupVm {
  type: string;
  labelKey: string;
  hits: HitVm[];
}

@Component({
  selector: 'app-global-search',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, IconComponent, TranslatePipe],
  template: `
    <div class="gsearch" [class.gsearch--open]="open()">
      <app-icon class="gsearch__icon" aria-hidden="true" name="search" />
      <input
        #box
        class="gsearch__input"
        type="text"
        role="combobox"
        autocomplete="off"
        [attr.aria-expanded]="open()"
        aria-controls="gsearch-panel"
        [formControl]="query"
        [placeholder]="'search.placeholder' | t"
        (focus)="onFocus()"
        (keydown.arrowDown)="move(1, $event)"
        (keydown.arrowUp)="move(-1, $event)"
        (keydown.enter)="onEnter($event)"
        (keydown.escape)="close()"
      />
      @if (query.value) {
        <button
          type="button"
          class="gsearch__clear"
          [attr.aria-label]="'search.clear' | t"
          (click)="clear()"
        >
          <app-icon aria-hidden="true" name="close" />
        </button>
      }

      @if (open()) {
        <div id="gsearch-panel" class="gsearch__panel" role="listbox">
          @if (loading()) {
            <div class="gsearch__status">{{ 'search.searching' | t }}</div>
          } @else if (groups().length) {
            @for (group of groups(); track group.type) {
              <div class="gsearch__group-label">{{ group.labelKey | t }}</div>
              @for (hit of group.hits; track hit.id) {
                <button
                  type="button"
                  role="option"
                  class="gsearch__hit"
                  [class.gsearch__hit--active]="hit.index === activeIndex()"
                  [attr.aria-selected]="hit.index === activeIndex()"
                  (mouseenter)="activeIndex.set(hit.index)"
                  (click)="go(hit)"
                >
                  <app-icon class="gsearch__hit-icon" aria-hidden="true" [name]="hit.icon" />
                  <span class="gsearch__hit-text">
                    <span class="gsearch__hit-title">{{ hit.title }}</span>
                    @if (hit.subtitle) {
                      <span class="gsearch__hit-sub">{{ hit.subtitle }}</span>
                    }
                  </span>
                </button>
              }
            }
          } @else if (query.value.trim().length >= minTerm) {
            <div class="gsearch__status">{{ 'search.noResults' | t }}</div>
          } @else {
            <div class="gsearch__status">{{ 'search.hint' | t }}</div>
          }
        </div>
      }
    </div>
  `,
  styleUrl: './global-search.component.scss',
})
export class GlobalSearchComponent {
  private readonly service = inject(SearchService);
  private readonly router = inject(Router);
  private readonly host = inject(ElementRef<HTMLElement>);

  readonly minTerm = MIN_TERM;
  readonly query = new FormControl('', { nonNullable: true });
  readonly open = signal(false);
  readonly loading = signal(false);
  readonly activeIndex = signal(-1);
  private readonly hits = signal<SearchHit[]>([]);

  readonly groups = computed<GroupVm[]>(() => {
    const byType = new Map<string, HitVm[]>();
    let index = 0;
    // Preserve HIT_ORDER; assign a running flat index for keyboard navigation.
    for (const type of HIT_ORDER) {
      const forType = this.hits().filter((h) => h.type === type);
      if (!forType.length) {
        continue;
      }
      const cfg = HIT_CONFIG[type];
      byType.set(
        type,
        forType.map((h) => ({ ...h, index: index++, icon: cfg.icon, path: cfg.path })),
      );
    }
    return HIT_ORDER.filter((t) => byType.has(t)).map((t) => ({
      type: t,
      labelKey: HIT_CONFIG[t].labelKey,
      hits: byType.get(t)!,
    }));
  });

  private readonly flatHits = computed<HitVm[]>(() =>
    this.groups().flatMap((g) => g.hits),
  );

  constructor() {
    this.query.valueChanges
      .pipe(
        debounceTime(250),
        map((v) => v.trim()),
        distinctUntilChanged(),
        switchMap((term) => {
          this.activeIndex.set(-1);
          if (term.length < MIN_TERM) {
            this.hits.set([]);
            this.loading.set(false);
            return of(null);
          }
          this.loading.set(true);
          this.open.set(true);
          return this.service.search(term).pipe(catchError(() => of({ hits: [] })));
        }),
        takeUntilDestroyed(),
      )
      .subscribe((result) => {
        if (result) {
          this.hits.set(result.hits);
          this.loading.set(false);
        }
      });
  }

  onFocus(): void {
    if (this.query.value.trim().length >= MIN_TERM || this.hits().length) {
      this.open.set(true);
    }
  }

  move(delta: number, event: Event): void {
    if (!this.open()) {
      return;
    }
    event.preventDefault();
    const count = this.flatHits().length;
    if (!count) {
      return;
    }
    const next = Math.max(0, Math.min(count - 1, this.activeIndex() + delta));
    this.activeIndex.set(next);
  }

  onEnter(event: Event): void {
    const active = this.flatHits()[this.activeIndex()];
    if (active) {
      event.preventDefault();
      this.go(active);
    }
  }

  go(hit: HitVm): void {
    void this.router.navigate([hit.path, hit.id]);
    this.close();
  }

  clear(): void {
    this.query.setValue('');
    this.hits.set([]);
    this.open.set(false);
  }

  close(): void {
    this.open.set(false);
    this.activeIndex.set(-1);
  }

  /** Close when a click lands outside the search widget. */
  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (this.open() && !this.host.nativeElement.contains(event.target as Node)) {
      this.close();
    }
  }
}
