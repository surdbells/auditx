import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';

import { IconComponent } from '../../../core/icons/icon.component';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { ReportSignOffQueueItem } from '../../../core/models';
import { PageGuide } from '../../../core/models/page-guide.models';
import { AuthService } from '../../../core/services/auth.service';
import { ReportsService } from '../../../core/services/reports.service';
import { UserLookupService } from '../../../core/services/user-lookup.service';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { PageGuideComponent } from '../../../shared/components/page-guide/page-guide.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';

type ViewState = 'loading' | 'ready' | 'error';

const SIGN_OFF_QUEUE_GUIDE: PageGuide = {
  id: 'report-sign-off-queue',
  titleKey: 'reports.signOffQueue.title',
  purposeKey: 'reports.signOffQueue.guide.purpose',
  descriptionKey: 'reports.signOffQueue.guide.description',
  actionKeys: ['reports.signOffQueue.guide.action.open', 'reports.signOffQueue.guide.action.decide'],
  sections: [
    {
      selector: '.queue',
      titleKey: 'reports.signOffQueue.guide.section.list.title',
      bodyKey: 'reports.signOffQueue.guide.section.list.body',
    },
  ],
  workflowKeys: [
    'reports.signOffQueue.guide.flow.draft',
    'reports.signOffQueue.guide.flow.final',
    'reports.signOffQueue.guide.flow.decide',
    'reports.signOffQueue.guide.flow.distribute',
  ],
  dependsOnKeys: ['reports.signOffQueue.guide.dep.reports'],
  usedByKeys: ['reports.signOffQueue.guide.use.distribution'],
  businessRuleKeys: ['reports.signOffQueue.guide.rule.preparer', 'reports.signOffQueue.guide.rule.returned'],
  tipKeys: ['reports.signOffQueue.guide.tip.read'],
  permissionKeys: ['reports.signOffQueue.guide.perm.cia'],
  faq: [
    { questionKey: 'reports.signOffQueue.guide.faq.own.q', answerKey: 'reports.signOffQueue.guide.faq.own.a' },
  ],
};

/**
 * The Chief Internal Auditor's queue: final engagement reports waiting for sign-off, oldest first. Each opens the
 * report, where it is read and then signed off or returned for changes.
 */
@Component({
  selector: 'app-sign-off-queue',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    RouterLink,
    MatButtonModule,
    MatCardModule,
    IconComponent,
    TranslatePipe,
    EmptyStateComponent,
    ErrorStateComponent,
    LoadingComponent,
    PageGuideComponent,
    PageHeaderComponent,
  ],
  template: `
    <app-page-header [title]="'reports.signOffQueue.title' | t" [subtitle]="'reports.signOffQueue.subtitle' | t" />
    <app-page-guide [guide]="guide" />

    @switch (state()) {
      @case ('loading') {
        <app-loading [message]="'reports.signOffQueue.loading' | t" />
      }
      @case ('error') {
        <app-error-state [message]="'reports.signOffQueue.error' | t" (retry)="load()" />
      }
      @case ('ready') {
        @if (items().length === 0) {
          <app-empty-state
            icon="verified"
            [title]="'reports.signOffQueue.empty.title' | t"
            [message]="'reports.signOffQueue.empty.message' | t"
          />
        } @else {
          <ul class="queue">
            @for (item of items(); track item.reportId) {
              <li>
                <mat-card appearance="outlined" class="queue__item">
                  <div class="queue__main">
                    <h3 class="queue__title">{{ item.auditName }}</h3>
                    <p class="queue__meta">
                      {{ 'reports.signOffQueue.meta' | t: { version: item.versionNumber, name: nameOf(item.generatedBy) } }}
                      @if (item.completedAt) {
                        · {{ item.completedAt | date: 'mediumDate' }}
                      }
                    </p>
                    @if (item.generatedBy === myUserId()) {
                      <p class="queue__note">{{ 'reports.signOffQueue.yours' | t }}</p>
                    }
                  </div>
                  <a matButton="filled" [routerLink]="['/reports', item.reportId]">
                    <app-icon name="rate_review" />
                    {{ 'reports.signOffQueue.review' | t }}
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
    .queue {
      list-style: none;
      margin: 0;
      padding: 0;
      display: flex;
      flex-direction: column;
      gap: 0.75rem;
    }
    .queue__item {
      display: flex;
      flex-direction: row;
      align-items: center;
      gap: 1rem;
      padding: 1rem 1.25rem;
    }
    .queue__main {
      flex: 1;
      min-width: 0;
    }
    .queue__title {
      margin: 0;
      font-size: 1rem;
      font-weight: 600;
    }
    .queue__meta,
    .queue__note {
      margin: 0.25rem 0 0;
      font-size: 0.875rem;
      color: var(--mat-sys-on-surface-variant);
    }
    @media (max-width: 600px) {
      .queue__item {
        flex-direction: column;
        align-items: stretch;
      }
    }
  `,
})
export class SignOffQueueComponent {
  private readonly service = inject(ReportsService);
  private readonly userLookup = inject(UserLookupService);
  private readonly auth = inject(AuthService);

  readonly state = signal<ViewState>('loading');
  readonly items = signal<ReportSignOffQueueItem[]>([]);
  readonly myUserId = computed(() => this.auth.session()?.userId ?? null);
  readonly guide = SIGN_OFF_QUEUE_GUIDE;

  constructor() {
    queueMicrotask(() => this.load());
  }

  load(): void {
    this.state.set('loading');
    this.service.awaitingSignOff().subscribe({
      next: (items) => {
        this.items.set(items);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  nameOf(userId: string): string {
    return this.userLookup.displayName(userId);
  }
}
