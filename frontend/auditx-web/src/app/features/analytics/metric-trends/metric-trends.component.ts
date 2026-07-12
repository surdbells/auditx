import { DatePipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { IconComponent } from '../../../core/icons/icon.component';
import { MatSelectModule } from '@angular/material/select';
import { RouterLink } from '@angular/router';

import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { AnalyticsService } from '../../../core/services/analytics.service';
import { ComparisonPeriodType, MetricComparison } from '../../../core/models';
import { PointDatum } from '../../../shared/charts/chart-types';
import { LineChartComponent } from '../../../shared/charts/line-chart/line-chart.component';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { PageGuideComponent } from '../../../shared/components/page-guide/page-guide.component';
import { PageGuide } from '../../../core/models/page-guide.models';

type ViewState = 'loading' | 'ready' | 'error';

/** Contextual page guide for the metric-trends view (drives the walkthrough + the About panel). */
const TRENDS_GUIDE: PageGuide = {
  id: 'metric-trends',
  titleKey: 'trends.title',
  purposeKey: 'trends.guide.purpose',
  descriptionKey: 'trends.guide.description',
  actionKeys: [
    'trends.guide.action.metric',
    'trends.guide.action.period',
    'trends.guide.action.forecast',
    'trends.guide.action.refresh',
  ],
  sections: [
    { selector: '.mt__controls', titleKey: 'trends.guide.section.controls.title', bodyKey: 'trends.guide.section.controls.body' },
    { selector: '.mt__stats', titleKey: 'trends.guide.section.stats.title', bodyKey: 'trends.guide.section.stats.body' },
    { selector: '.mt__chart-card', titleKey: 'trends.guide.section.chart.title', bodyKey: 'trends.guide.section.chart.body' },
  ],
  workflowKeys: ['trends.guide.flow.capture', 'trends.guide.flow.bucket', 'trends.guide.flow.compare', 'trends.guide.flow.forecast', 'trends.guide.flow.decide'],
  dependsOnKeys: ['trends.guide.dep.snapshots', 'trends.guide.dep.audits', 'trends.guide.dep.exceptions'],
  usedByKeys: ['trends.guide.use.dashboards', 'trends.guide.use.reports', 'trends.guide.use.review'],
  businessRuleKeys: ['trends.guide.rule.snapshot', 'trends.guide.rule.forecast', 'trends.guide.rule.series', 'trends.guide.rule.grain'],
  tipKeys: ['trends.guide.tip.grain', 'trends.guide.tip.metric', 'trends.guide.tip.delta'],
  permissionKeys: ['trends.guide.perm.viewer', 'trends.guide.perm.manager', 'trends.guide.perm.admin'],
  faq: [
    { questionKey: 'trends.guide.faq.empty.q', answerKey: 'trends.guide.faq.empty.a' },
    { questionKey: 'trends.guide.faq.forecast.q', answerKey: 'trends.guide.faq.forecast.a' },
  ],
};

/** A selectable snapshot metric (backend AnalyticsMetricKeys) with its display label. */
interface MetricOption {
  key: string;
  labelKey: string;
}

/**
 * Metric trends (D1): period-over-period comparison (MoM / QoQ / YoY) + a linear forecast for a KPI metric,
 * charted from the daily analytics-snapshot fact table. Pick a metric + grain; the view shows the trailing
 * period buckets, the latest-vs-previous delta, and the projected next-period value.
 */
@Component({
  selector: 'app-metric-trends',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    RouterLink,
    MatCardModule,
    MatButtonModule,
    MatButtonToggleModule,
    MatFormFieldModule,
    MatSelectModule,
    IconComponent,
    TranslatePipe,
    LineChartComponent,
    LoadingComponent,
    ErrorStateComponent,
    EmptyStateComponent,
    PageHeaderComponent,
    PageGuideComponent,
  ],
  templateUrl: './metric-trends.component.html',
  styleUrl: './metric-trends.component.scss',
})
export class MetricTrendsComponent {
  private readonly service = inject(AnalyticsService);

  /** Contextual guide metadata (walkthrough + About panel). */
  readonly guide = TRENDS_GUIDE;

  /** The snapshot metrics offered in the selector (must exist in the backend AnalyticsMetricKeys). */
  readonly metrics: readonly MetricOption[] = [
    { key: 'exceptions.total_open', labelKey: 'trends.metric.exceptionsOpen' },
    { key: 'function.open_exception_backlog', labelKey: 'trends.metric.openBacklog' },
    { key: 'function.closed_exceptions', labelKey: 'trends.metric.closedExceptions' },
    { key: 'function.closure_rate_pct', labelKey: 'trends.metric.closureRate' },
    { key: 'exceptions.avg_closure_days', labelKey: 'trends.metric.avgClosureDays' },
    { key: 'function.audits_in_flight', labelKey: 'trends.metric.auditsInFlight' },
    { key: 'function.audits_completed', labelKey: 'trends.metric.auditsCompleted' },
    { key: 'function.plan_execution_pct', labelKey: 'trends.metric.planExecution' },
    { key: 'plan.completion_pct', labelKey: 'trends.metric.planCompletion' },
    { key: 'risk.open_total', labelKey: 'trends.metric.riskOpen' },
    { key: 'risk.overdue_review', labelKey: 'trends.metric.riskOverdue' },
    { key: 'risk.avg_current_score', labelKey: 'trends.metric.riskAvgScore' },
  ];

  readonly periods: readonly ComparisonPeriodType[] = ['month', 'quarter', 'year'];

  readonly metric = signal<string>(this.metrics[0].key);
  readonly period = signal<ComparisonPeriodType>('month');
  readonly state = signal<ViewState>('loading');
  readonly comparison = signal<MetricComparison | null>(null);

  /** The period buckets as an ordered series for the line chart (gaps → NaN so the chart skips them). */
  readonly chartData = computed<PointDatum[]>(() =>
    (this.comparison()?.periods ?? []).map((p) => ({
      label: p.label,
      value: p.value ?? Number.NaN,
    })),
  );

  /** True once at least two valued buckets exist — otherwise the delta/forecast panel is not meaningful. */
  readonly hasSeries = computed(
    () => (this.comparison()?.periods ?? []).some((p) => p.value !== null),
  );

  /** Direction of the latest-vs-previous move, for the delta chip. */
  readonly deltaDirection = computed<'up' | 'down' | 'flat'>(() => {
    const d = this.comparison()?.delta;
    if (d === null || d === undefined || d === 0) {
      return 'flat';
    }
    return d > 0 ? 'up' : 'down';
  });

  constructor() {
    this.fetch();
  }

  selectMetric(key: string): void {
    this.metric.set(key);
    this.fetch();
  }

  selectPeriod(period: ComparisonPeriodType): void {
    this.period.set(period);
    this.fetch();
  }

  fetch(): void {
    this.state.set('loading');
    this.service.metricComparison(this.metric(), this.period()).subscribe({
      next: (result) => {
        this.comparison.set(result);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  /** Render a nullable metric value, falling back to an em dash. */
  valueOf(value: number | null | undefined): string {
    return value === null || value === undefined ? '—' : String(value);
  }
}
