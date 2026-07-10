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
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';

import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { AnalyticsService } from '../../../core/services/analytics.service';
import { RiskHeatmap, RiskRegisterSummary } from '../../../core/models';
import { BAND_COLOR, bandOf } from '../../risks/risk-format';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { PageGuideComponent } from '../../../shared/components/page-guide/page-guide.component';
import { PageGuide } from '../../../core/models/page-guide.models';

type ViewState = 'loading' | 'ready' | 'error';

/** Contextual page guide for the risk heat-map (drives the walkthrough + the About panel). */
const RISK_HEATMAP_GUIDE: PageGuide = {
  id: 'risk-heatmap',
  titleKey: 'risk.heatmap.title',
  purposeKey: 'analytics.riskHeatmap.guide.purpose',
  descriptionKey: 'analytics.riskHeatmap.guide.description',
  actionKeys: [
    'analytics.riskHeatmap.guide.action.read',
    'analytics.riskHeatmap.guide.action.summary',
    'analytics.riskHeatmap.guide.action.register',
    'analytics.riskHeatmap.guide.action.refresh',
  ],
  sections: [
    { selector: '.rh__summary-card', titleKey: 'analytics.riskHeatmap.guide.section.summary.title', bodyKey: 'analytics.riskHeatmap.guide.section.summary.body' },
    { selector: '.rh__grid-card', titleKey: 'analytics.riskHeatmap.guide.section.grid.title', bodyKey: 'analytics.riskHeatmap.guide.section.grid.body' },
    { selector: '[data-guide="register"]', titleKey: 'analytics.riskHeatmap.guide.section.register.title', bodyKey: 'analytics.riskHeatmap.guide.section.register.body' },
  ],
  workflowKeys: [
    'analytics.riskHeatmap.guide.flow.identify',
    'analytics.riskHeatmap.guide.flow.assess',
    'analytics.riskHeatmap.guide.flow.heatmap',
    'analytics.riskHeatmap.guide.flow.treat',
    'analytics.riskHeatmap.guide.flow.review',
  ],
  dependsOnKeys: [
    'analytics.riskHeatmap.guide.dep.register',
    'analytics.riskHeatmap.guide.dep.assessment',
    'analytics.riskHeatmap.guide.dep.owners',
    'analytics.riskHeatmap.guide.dep.bands',
  ],
  usedByKeys: [
    'analytics.riskHeatmap.guide.use.planning',
    'analytics.riskHeatmap.guide.use.findings',
    'analytics.riskHeatmap.guide.use.reports',
    'analytics.riskHeatmap.guide.use.dashboards',
  ],
  businessRuleKeys: [
    'analytics.riskHeatmap.guide.rule.score',
    'analytics.riskHeatmap.guide.rule.open',
    'analytics.riskHeatmap.guide.rule.band',
    'analytics.riskHeatmap.guide.rule.overdue',
  ],
  tipKeys: [
    'analytics.riskHeatmap.guide.tip.corner',
    'analytics.riskHeatmap.guide.tip.hover',
    'analytics.riskHeatmap.guide.tip.act',
  ],
  permissionKeys: [
    'analytics.riskHeatmap.guide.perm.analytics',
    'analytics.riskHeatmap.guide.perm.riskManager',
    'analytics.riskHeatmap.guide.perm.admin',
  ],
  faq: [
    { questionKey: 'analytics.riskHeatmap.guide.faq.empty.q', answerKey: 'analytics.riskHeatmap.guide.faq.empty.a' },
    { questionKey: 'analytics.riskHeatmap.guide.faq.colour.q', answerKey: 'analytics.riskHeatmap.guide.faq.colour.a' },
  ],
};

/** Enterprise risk heatmap + register summary (P1-A, ViewAnalytics). */
@Component({
  selector: 'app-risk-heatmap',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    TranslatePipe,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PageHeaderComponent,
    PageGuideComponent,
  ],
  templateUrl: './risk-heatmap.component.html',
  styleUrl: './risk-heatmap.component.scss',
})
export class RiskHeatmapComponent {
  private readonly service = inject(AnalyticsService);

  /** Rows render impact 5→1 (high at top); columns render likelihood 1→5. */
  readonly impacts = [5, 4, 3, 2, 1];
  readonly likelihoods = [1, 2, 3, 4, 5];

  readonly guide = RISK_HEATMAP_GUIDE;

  readonly state = signal<ViewState>('loading');
  readonly heatmap = signal<RiskHeatmap | null>(null);
  readonly summary = signal<RiskRegisterSummary | null>(null);

  /** "likelihood-impact" → count, for O(1) cell lookup. */
  private readonly countByCell = computed(() => {
    const map = new Map<string, number>();
    for (const c of this.heatmap()?.cells ?? []) {
      map.set(`${c.likelihood}-${c.impact}`, c.count);
    }
    return map;
  });

  readonly isEmpty = computed(() => this.state() === 'ready' && (this.heatmap()?.totalOpen ?? 0) === 0);

  constructor() {
    this.fetch();
  }

  fetch(): void {
    this.state.set('loading');
    forkJoin({ heatmap: this.service.riskHeatmap(), summary: this.service.riskSummary() }).subscribe({
      next: ({ heatmap, summary }) => {
        this.heatmap.set(heatmap);
        this.summary.set(summary);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  count(likelihood: number, impact: number): number {
    return this.countByCell().get(`${likelihood}-${impact}`) ?? 0;
  }

  /** Cell background: band colour at full strength when populated, a faint tint when empty. */
  cellStyle(likelihood: number, impact: number): Record<string, string> {
    const colour = BAND_COLOR[bandOf(likelihood * impact)];
    const populated = this.count(likelihood, impact) > 0;
    return {
      background: `color-mix(in srgb, ${colour} ${populated ? '82%' : '12%'}, transparent)`,
      color: populated ? '#fff' : 'var(--mat-sys-on-surface-variant)',
    };
  }
}
