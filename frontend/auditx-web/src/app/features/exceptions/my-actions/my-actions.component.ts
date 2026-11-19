import { ChangeDetectionStrategy, Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';

import { IconComponent } from '../../../core/icons/icon.component';
import { ExceptionsService } from '../../../core/services/exceptions.service';
import { MyActionItem, MyActionKind } from '../../../core/models';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';

type ViewState = 'loading' | 'ready' | 'error';

/** Icon per kind of work, so the list reads at a glance. */
const KIND_ICON: Record<MyActionKind, string> = {
  respond: 'rate_review',
  submit_plan: 'playlist_add',
  complete_action: 'task_alt',
  complete_plan: 'checklist',
  decide_extension: 'schedule',
};

/**
 * "My actions": everything on findings still being remediated that waits on the signed-in user — answering a finding,
 * writing its action plan, finishing an action, marking the plan complete, or deciding a request for more time on a
 * finding they raised. Overdue first, then soonest due. Each item opens the finding, where the work is done.
 */
@Component({
  selector: 'app-my-actions',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    RouterLink,
    MatCardModule,
    MatButtonModule,
    IconComponent,
    PageHeaderComponent,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    TranslatePipe,
  ],
  template: `
    <app-page-header [title]="'myActions.title' | t" [subtitle]="'myActions.subtitle' | t" />

    @switch (state()) {
      @case ('loading') {
        <app-loading [message]="'myActions.loading' | t" />
      }
      @case ('error') {
        <app-error-state [message]="'myActions.loadError' | t" (retry)="refresh()" />
      }
      @case ('ready') {
        @if (!items().length) {
          <app-empty-state icon="task_alt" [title]="'myActions.empty.title' | t" [message]="'myActions.empty.message' | t" />
        } @else {
          <div class="summary" role="list">
            <div class="summary__tile" role="listitem" [class.summary__tile--alert]="overdueCount() > 0">
              <span class="summary__value">{{ overdueCount() }}</span>
              <span class="summary__label">{{ 'myActions.summary.overdue' | t }}</span>
            </div>
            <div class="summary__tile" role="listitem">
              <span class="summary__value">{{ dueSoonCount() }}</span>
              <span class="summary__label">{{ 'myActions.summary.dueSoon' | t }}</span>
            </div>
            <div class="summary__tile" role="listitem">
              <span class="summary__value">{{ items().length }}</span>
              <span class="summary__label">{{ 'myActions.summary.total' | t }}</span>
            </div>
          </div>

          <ul class="list">
            @for (item of items(); track trackKey(item)) {
              <li>
                <mat-card appearance="outlined" class="item" [class.item--overdue]="item.isOverdue">
                  <app-icon class="item__icon" [name]="iconFor(item.kind)" />
                  <div class="item__body">
                    <p class="item__kind">{{ 'myActions.kind.' + item.kind | t }}</p>
                    <a class="item__title" [routerLink]="['/exceptions', item.exceptionId]">{{ item.exceptionTitle }}</a>
                    @if (item.description) {
                      <p class="item__desc">{{ item.description }}</p>
                    }
                    <p class="item__meta">
                      <span class="severity-badge" [attr.data-severity]="item.severity">{{ item.severity }}</span>
                      @if (item.dueDate) {
                        <span class="item__due" [class.item__due--overdue]="item.isOverdue">
                          @if (item.isOverdue) {
                            {{ 'myActions.overdueBy' | t: { days: -(item.daysUntilDue ?? 0), date: (item.dueDate | date: 'mediumDate') ?? '' } }}
                          } @else if (item.daysUntilDue === 0) {
                            {{ 'myActions.dueToday' | t }}
                          } @else {
                            {{ 'myActions.dueIn' | t: { days: item.daysUntilDue ?? 0, date: (item.dueDate | date: 'mediumDate') ?? '' } }}
                          }
                        </span>
                      }
                      @if (item.extensionPending) {
                        <span class="status-badge" data-status="map_submitted">{{ 'exceptions.extension.pendingBadge' | t }}</span>
                      }
                    </p>
                  </div>
                  <a matButton="tonal" class="item__open" [routerLink]="['/exceptions', item.exceptionId]">
                    {{ 'myActions.open' | t }}
                  </a>
                </mat-card>
              </li>
            }
          </ul>
        }
      }
    }
  `,
  styles: `
    :host { display: block; }
    .summary {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(140px, 1fr));
      gap: 0.75rem;
      margin-bottom: 1.25rem;
    }
    .summary__tile {
      display: flex;
      flex-direction: column;
      gap: 0.15rem;
      padding: 0.85rem 1rem;
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: 12px;
      background: var(--mat-sys-surface-container-low);
    }
    .summary__tile--alert { border-color: var(--mat-sys-error); }
    .summary__tile--alert .summary__value { color: var(--mat-sys-error); }
    .summary__value { font: var(--mat-sys-headline-small); font-variant-numeric: tabular-nums; }
    .summary__label { font: var(--mat-sys-label-medium); color: var(--mat-sys-on-surface-variant); }

    .list { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 0.75rem; }
    .item {
      display: flex;
      flex-direction: row;
      align-items: flex-start;
      gap: 0.9rem;
      padding: 0.9rem 1.1rem;
    }
    .item--overdue { border-left: 4px solid var(--mat-sys-error); }
    .item__icon { color: var(--mat-sys-primary); margin-top: 0.15rem; flex-shrink: 0; }
    .item__body { flex: 1; min-width: 0; display: flex; flex-direction: column; gap: 0.2rem; }
    .item__kind {
      margin: 0;
      font: var(--mat-sys-label-medium);
      letter-spacing: 0.02em;
      text-transform: uppercase;
      color: var(--mat-sys-on-surface-variant);
    }
    .item__title { font-weight: 600; color: var(--mat-sys-on-surface); text-decoration: none; overflow-wrap: anywhere; }
    .item__title:hover, .item__title:focus-visible { text-decoration: underline; }
    .item__desc { margin: 0; color: var(--mat-sys-on-surface-variant); overflow-wrap: anywhere; }
    .item__meta { margin: 0.25rem 0 0; display: flex; flex-wrap: wrap; align-items: center; gap: 0.5rem; }
    .item__due { font: var(--mat-sys-label-medium); font-variant-numeric: tabular-nums; }
    .item__due--overdue { color: var(--mat-sys-error); font-weight: 600; }
    .item__open { flex-shrink: 0; align-self: center; }
    @media (max-width: 600px) {
      .item { flex-wrap: wrap; }
      .item__open { width: 100%; }
    }
  `,
})
export class MyActionsComponent {
  private readonly service = inject(ExceptionsService);

  readonly state = signal<ViewState>('loading');
  readonly items = signal<MyActionItem[]>([]);

  readonly overdueCount = computed(() => this.items().filter((i) => i.isOverdue).length);
  readonly dueSoonCount = computed(
    () => this.items().filter((i) => !i.isOverdue && i.daysUntilDue != null && i.daysUntilDue <= 7).length,
  );

  constructor() {
    this.refresh();

    // Work arrives while the page is open (plans approved, requests for more time): refresh quietly on focus.
    const quietly = () => {
      if (document.visibilityState === 'visible' && this.state() === 'ready') {
        this.refresh(true);
      }
    };
    window.addEventListener('focus', quietly);
    inject(DestroyRef).onDestroy(() => window.removeEventListener('focus', quietly));
  }

  refresh(quiet = false): void {
    if (!quiet) {
      this.state.set('loading');
    }
    this.service.myActions().subscribe({
      next: (rows) => {
        this.items.set(rows);
        this.state.set('ready');
      },
      error: () => {
        if (!quiet) {
          this.state.set('error');
        }
      },
    });
  }

  iconFor(kind: MyActionKind): string {
    return KIND_ICON[kind] ?? 'task_alt';
  }

  trackKey(item: MyActionItem): string {
    return `${item.kind}:${item.exceptionId}:${item.actionId ?? ''}:${item.extensionRequestId ?? ''}`;
  }
}
