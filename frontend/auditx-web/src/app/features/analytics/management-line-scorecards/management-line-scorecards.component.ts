import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';

import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { AnalyticsService } from '../../../core/services/analytics.service';
import { ManagementLineScorecard } from '../../../core/models';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { PageGuideComponent } from '../../../shared/components/page-guide/page-guide.component';
import { PageGuide } from '../../../core/models/page-guide.models';

type ViewState = 'loading' | 'ready' | 'error';

/** Contextual page guide for the management-line scorecards (drives the walkthrough + the About panel). */
const MANAGEMENT_LINE_GUIDE: PageGuide = {
  id: 'analytics-management-line',
  titleKey: 'analytics.managementLine.title',
  purposeKey: 'analytics.managementLine.guide.purpose',
  descriptionKey: 'analytics.managementLine.guide.description',
  actionKeys: [
    'analytics.managementLine.guide.action.review',
    'analytics.managementLine.guide.action.drill',
  ],
  sections: [
    { selector: '.mline__summary', titleKey: 'analytics.managementLine.guide.section.summary.title', bodyKey: 'analytics.managementLine.guide.section.summary.body' },
    { selector: '.mline__table', titleKey: 'analytics.managementLine.guide.section.table.title', bodyKey: 'analytics.managementLine.guide.section.table.body' },
  ],
  businessRuleKeys: [
    'analytics.managementLine.guide.rule.rollup',
    'analytics.managementLine.guide.rule.fallback',
  ],
  tipKeys: ['analytics.managementLine.guide.tip.overdue'],
  permissionKeys: ['analytics.managementLine.guide.perm.view'],
};

/**
 * Management-line scorecards (ViewAnalytics). One row per manager (anyone with reports), rolling up open + overdue
 * findings across their whole reporting subtree — the reporting-line analogue of the org-unit scorecards. The
 * reporting line is the explicit line manager, else the head of the user's org unit.
 */
@Component({
  selector: 'app-management-line-scorecards',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    MatCardModule,
    MatTableModule,
    TranslatePipe,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PageHeaderComponent,
    PageGuideComponent,
  ],
  templateUrl: './management-line-scorecards.component.html',
  styleUrl: './management-line-scorecards.component.scss',
})
export class ManagementLineScorecardsComponent {
  private readonly service = inject(AnalyticsService);

  readonly guide = MANAGEMENT_LINE_GUIDE;

  readonly displayedColumns = ['manager', 'directReports', 'totalReports', 'open', 'overdue'];

  readonly state = signal<ViewState>('loading');
  readonly scorecards = signal<ManagementLineScorecard[]>([]);

  readonly isEmpty = computed(
    () => this.state() === 'ready' && this.scorecards().length === 0,
  );

  /** Managers count + how many have any overdue finding in their team (no finding sums — subtrees overlap). */
  readonly totals = computed(() => {
    const rows = this.scorecards();
    return {
      managers: rows.length,
      withOverdue: rows.filter((r) => r.overdueFindings > 0).length,
    };
  });

  constructor() {
    this.fetch();
  }

  fetch(): void {
    this.state.set('loading');
    this.service.managementLineScorecards().subscribe({
      next: (rows) => {
        this.scorecards.set(rows);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }
}
