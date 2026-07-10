import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';

import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { AnalyticsService } from '../../../core/services/analytics.service';
import { UserLookupService } from '../../../core/services/user-lookup.service';
import { PerformanceScorecard } from '../../../core/models';
import { days } from '../format';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { PageGuideComponent } from '../../../shared/components/page-guide/page-guide.component';
import { PageGuide } from '../../../core/models/page-guide.models';

type ViewState = 'loading' | 'ready' | 'error';

/** Contextual page guide for the performance scorecards view (drives the walkthrough + the About panel). */
const SCORECARDS_GUIDE: PageGuide = {
  id: 'performance-scorecards',
  titleKey: 'analytics.scorecards.title',
  purposeKey: 'analytics.performanceScorecards.guide.purpose',
  descriptionKey: 'analytics.performanceScorecards.guide.description',
  actionKeys: [
    'analytics.performanceScorecards.guide.action.review',
    'analytics.performanceScorecards.guide.action.compare',
    'analytics.performanceScorecards.guide.action.drill',
    'analytics.performanceScorecards.guide.action.refresh',
  ],
  sections: [
    { selector: '[data-guide="controls"]', titleKey: 'analytics.performanceScorecards.guide.section.controls.title', bodyKey: 'analytics.performanceScorecards.guide.section.controls.body' },
    { selector: '.scorecards__table-card', titleKey: 'analytics.performanceScorecards.guide.section.card.title', bodyKey: 'analytics.performanceScorecards.guide.section.card.body' },
    { selector: '.scorecards__table', titleKey: 'analytics.performanceScorecards.guide.section.table.title', bodyKey: 'analytics.performanceScorecards.guide.section.table.body' },
  ],
  workflowKeys: [
    'analytics.performanceScorecards.guide.flow.lead',
    'analytics.performanceScorecards.guide.flow.deliver',
    'analytics.performanceScorecards.guide.flow.raise',
    'analytics.performanceScorecards.guide.flow.aggregate',
    'analytics.performanceScorecards.guide.flow.review',
  ],
  dependsOnKeys: [
    'analytics.performanceScorecards.guide.dep.audits',
    'analytics.performanceScorecards.guide.dep.exceptions',
    'analytics.performanceScorecards.guide.dep.users',
  ],
  usedByKeys: [
    'analytics.performanceScorecards.guide.use.appraisal',
    'analytics.performanceScorecards.guide.use.capacity',
    'analytics.performanceScorecards.guide.use.reports',
  ],
  businessRuleKeys: [
    'analytics.performanceScorecards.guide.rule.lead',
    'analytics.performanceScorecards.guide.rule.completed',
    'analytics.performanceScorecards.guide.rule.cycle',
    'analytics.performanceScorecards.guide.rule.closure',
  ],
  tipKeys: [
    'analytics.performanceScorecards.guide.tip.context',
    'analytics.performanceScorecards.guide.tip.closure',
  ],
  permissionKeys: [
    'analytics.performanceScorecards.guide.perm.view',
    'analytics.performanceScorecards.guide.perm.sensitive',
  ],
  faq: [
    { questionKey: 'analytics.performanceScorecards.guide.faq.empty.q', answerKey: 'analytics.performanceScorecards.guide.faq.empty.a' },
    { questionKey: 'analytics.performanceScorecards.guide.faq.ranking.q', answerKey: 'analytics.performanceScorecards.guide.faq.ranking.a' },
  ],
};

/** Per-audit-lead performance scorecards (PerformanceAnalyticsView). */
@Component({
  selector: 'app-performance-scorecards',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    TranslatePipe,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PageHeaderComponent,
    PageGuideComponent,
  ],
  templateUrl: './performance-scorecards.component.html',
  styleUrl: './performance-scorecards.component.scss',
})
export class PerformanceScorecardsComponent {
  private readonly service = inject(AnalyticsService);
  /** Resolves audit-lead user ids to display names in the table. */
  readonly userLookup = inject(UserLookupService);

  readonly displayedColumns = [
    'lead',
    'auditsLed',
    'auditsCompleted',
    'cycleDays',
    'exceptionsRaised',
    'exceptionsClosed',
    'closureDays',
  ];

  readonly guide = SCORECARDS_GUIDE;

  readonly state = signal<ViewState>('loading');
  readonly scorecards = signal<PerformanceScorecard[]>([]);

  readonly days = days;

  readonly isEmpty = computed(
    () => this.state() === 'ready' && this.scorecards().length === 0,
  );

  constructor() {
    this.fetch();
  }

  fetch(): void {
    this.state.set('loading');
    this.service.performanceScorecards().subscribe({
      next: (rows) => {
        this.scorecards.set(rows);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }
}
