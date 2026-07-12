import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { IconComponent } from '../../../core/icons/icon.component';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';

import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { AnalyticsService } from '../../../core/services/analytics.service';
import { OrgUnitScorecard } from '../../../core/models';
import { days } from '../format';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { PageGuideComponent } from '../../../shared/components/page-guide/page-guide.component';
import { PageGuide } from '../../../core/models/page-guide.models';

type ViewState = 'loading' | 'ready' | 'error';

/** Contextual page guide for the org-unit scorecards (drives the walkthrough + the About panel). */
const ORG_SCORECARDS_GUIDE: PageGuide = {
  id: 'analytics-org-unit-scorecards',
  titleKey: 'analytics.orgUnits.title',
  purposeKey: 'analytics.orgScorecards.guide.purpose',
  descriptionKey: 'analytics.orgScorecards.guide.description',
  actionKeys: [
    'analytics.orgScorecards.guide.action.review',
    'analytics.orgScorecards.guide.action.compare',
    'analytics.orgScorecards.guide.action.drill',
    'analytics.orgScorecards.guide.action.refresh',
  ],
  sections: [
    { selector: '.orgcards__summary-card', titleKey: 'analytics.orgScorecards.guide.section.summary.title', bodyKey: 'analytics.orgScorecards.guide.section.summary.body' },
    { selector: '.orgcards__table', titleKey: 'analytics.orgScorecards.guide.section.table.title', bodyKey: 'analytics.orgScorecards.guide.section.table.body' },
    { selector: '.orgcards__unit', titleKey: 'analytics.orgScorecards.guide.section.hierarchy.title', bodyKey: 'analytics.orgScorecards.guide.section.hierarchy.body' },
  ],
  workflowKeys: [
    'analytics.orgScorecards.guide.flow.assign',
    'analytics.orgScorecards.guide.flow.audit',
    'analytics.orgScorecards.guide.flow.findings',
    'analytics.orgScorecards.guide.flow.rollup',
    'analytics.orgScorecards.guide.flow.act',
  ],
  dependsOnKeys: [
    'analytics.orgScorecards.guide.dep.orgUnits',
    'analytics.orgScorecards.guide.dep.audits',
    'analytics.orgScorecards.guide.dep.findings',
  ],
  usedByKeys: [
    'analytics.orgScorecards.guide.use.leadership',
    'analytics.orgScorecards.guide.use.planning',
    'analytics.orgScorecards.guide.use.reports',
  ],
  businessRuleKeys: [
    'analytics.orgScorecards.guide.rule.rollup',
    'analytics.orgScorecards.guide.rule.summary',
    'analytics.orgScorecards.guide.rule.closure',
  ],
  tipKeys: [
    'analytics.orgScorecards.guide.tip.critical',
    'analytics.orgScorecards.guide.tip.indent',
  ],
  permissionKeys: [
    'analytics.orgScorecards.guide.perm.view',
    'analytics.orgScorecards.guide.perm.orgUnits',
  ],
  faq: [
    { questionKey: 'analytics.orgScorecards.guide.faq.rollup.q', answerKey: 'analytics.orgScorecards.guide.faq.rollup.a' },
    { questionKey: 'analytics.orgScorecards.guide.faq.closure.q', answerKey: 'analytics.orgScorecards.guide.faq.closure.a' },
  ],
};

/**
 * Department / business-unit scorecards (ViewAnalytics). Each row aggregates an
 * org unit plus all of its descendants; rows arrive pre-order with a `depth` so
 * the tree can be rendered with indentation. The summary strip sums the root
 * rows only (each root already rolls up its whole subtree) to avoid double
 * counting nested units.
 */
@Component({
  selector: 'app-org-unit-scorecards',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    IconComponent,
    TranslatePipe,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PageHeaderComponent,
    PageGuideComponent,
  ],
  templateUrl: './org-unit-scorecards.component.html',
  styleUrl: './org-unit-scorecards.component.scss',
})
export class OrgUnitScorecardsComponent {
  private readonly service = inject(AnalyticsService);

  readonly guide = ORG_SCORECARDS_GUIDE;

  readonly displayedColumns = [
    'unit',
    'entities',
    'auditsCompleted',
    'auditsInFlight',
    'openFindings',
    'criticalOpen',
    'highOpen',
    'closedFindings',
    'closureDays',
  ];

  readonly state = signal<ViewState>('loading');
  readonly scorecards = signal<OrgUnitScorecard[]>([]);

  readonly days = days;

  readonly isEmpty = computed(
    () => this.state() === 'ready' && this.scorecards().length === 0,
  );

  /** Org-wide totals, summed over root units only (each root rolls up its subtree). */
  readonly totals = computed(() => {
    const roots = this.scorecards().filter((s) => s.depth === 0);
    return {
      units: this.scorecards().length,
      entities: roots.reduce((n, s) => n + s.entities, 0),
      openFindings: roots.reduce((n, s) => n + s.openFindings, 0),
      criticalOpen: roots.reduce((n, s) => n + s.criticalOpenFindings, 0),
    };
  });

  constructor() {
    this.fetch();
  }

  fetch(): void {
    this.state.set('loading');
    this.service.orgUnitScorecards().subscribe({
      next: (rows) => {
        this.scorecards.set(rows);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  /** Left indent (rem) for a unit's name cell, driven by its tree depth. */
  indent(depth: number): string {
    return `${depth * 1.25}rem`;
  }
}
