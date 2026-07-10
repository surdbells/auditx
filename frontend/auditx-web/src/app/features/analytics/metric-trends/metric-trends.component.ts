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
import { MatIconModule } from '@angular/material/icon';
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

type ViewState = 'loading' | 'ready' | 'error';

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
    MatIconModule,
    TranslatePipe,
    LineChartComponent,
    LoadingComponent,
    ErrorStateComponent,
    EmptyStateComponent,
    PageHeaderComponent,
  ],
  templateUrl: './metric-trends.component.html',
  styleUrl: './metric-trends.component.scss',
})
export class MetricTrendsComponent {
  private readonly service = inject(AnalyticsService);

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
