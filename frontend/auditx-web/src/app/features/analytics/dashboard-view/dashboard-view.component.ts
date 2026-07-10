import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  signal,
} from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';

import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';
import { AnalyticsService } from '../../../core/services/analytics.service';
import { NotificationService } from '../../../core/services/notification.service';
import { AuthService } from '../../../core/services/auth.service';
import { UserLookupService } from '../../../core/services/user-lookup.service';
import { EntityLookupService } from '../../../core/services/entity-lookup.service';
import { OrgUnitLookupService } from '../../../core/services/org-unit-lookup.service';
import { Permissions } from '../../../core/permissions';
import { DashboardDetail, DashboardWidget } from '../../../core/models';
import {
  BarDatum,
  KeyFigure,
  TableProjection,
  cellText,
  toBars,
  toKeyFigures,
  toTable,
} from '../widget-data';
import { humanise } from '../format';
import {
  BarChartComponent,
  ChartDatum,
  DonutChartComponent,
  GaugeChartComponent,
} from '../../../shared/charts';
import {
  WidgetDialogComponent,
  WidgetDialogResult,
} from '../dialogs/widget-dialog.component';
import {
  ConfirmDialogComponent,
  ConfirmDialogData,
} from '../../../shared/components/confirm-dialog/confirm-dialog.component';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';

type ViewState = 'loading' | 'ready' | 'error';

/**
 * Renders one dashboard generically: each widget is dispatched by `widgetType`
 * (single_metric / table / chart) against its computed `data`. A widget with
 * null data renders an empty shell with a "not available" note (the backend
 * degrades a forbidden widget to null). Charts are hand-rolled inline SVG
 * (app-bar-chart / app-donut-chart / app-gauge-chart) — no charting dependency.
 */
@Component({
  selector: 'app-dashboard-view',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    NgTemplateOutlet,
    RouterLink,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatTableModule,
    MatTooltipModule,
    TranslatePipe,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PageHeaderComponent,
    BarChartComponent,
    DonutChartComponent,
    GaugeChartComponent,
  ],
  templateUrl: './dashboard-view.component.html',
  styleUrl: './dashboard-view.component.scss',
})
export class DashboardViewComponent {
  /** Route param bound via withComponentInputBinding. */
  readonly idOrSlug = input.required<string>();

  private readonly service = inject(AnalyticsService);
  private readonly notify = inject(NotificationService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly i18n = inject(TranslationService);
  private readonly userLookup = inject(UserLookupService);
  private readonly entityLookup = inject(EntityLookupService);
  private readonly orgUnitLookup = inject(OrgUnitLookupService);

  /** Drilldown-only GUID id columns are not shown as text in a widget table. */
  private readonly hiddenColumns: ReadonlySet<string> = new Set([
    'id',
    'exceptionId',
    'auditId',
    'dashboardId',
    'widgetId',
  ]);

  /** GUID columns resolved to a human name via the shared lookups. */
  private readonly resolvers: Record<string, (id: string) => string> = {
    auditLeadUserId: (id) => this.userLookup.displayName(id),
    ownerUserId: (id) => this.userLookup.displayName(id),
    auditableEntityId: (id) => this.entityLookup.name(id),
  };

  private readonly headerOverrides: Record<string, string> = {
    auditLeadUserId: 'Audit lead',
    ownerUserId: 'Owner',
    auditableEntityId: 'Entity',
  };

  /** Columns whose snake_case enum values read better title-cased. */
  private readonly titleiseColumns: ReadonlySet<string> = new Set([
    'severity',
    'status',
    'category',
  ]);

  /** User-id columns that don't end in `UserId` (the `*By` actor fields), resolved to a name by convention. */
  private readonly userByColumns: ReadonlySet<string> = new Set([
    'createdBy', 'updatedBy', 'generatedBy', 'raisedBy', 'closedBy', 'approvedBy', 'deletedBy',
    'dispatchedBy', 'cancelledBy', 'activatedBy', 'performedBy', 'respondedBy', 'verifiedBy',
    'uploadedBy', 'ownerId', 'assignedTo',
  ]);

  readonly state = signal<ViewState>('loading');
  readonly dashboard = signal<DashboardDetail | null>(null);

  readonly humanise = humanise;

  readonly canConfigure = computed(() =>
    this.auth.hasPermission(Permissions.ConfigureDashboards),
  );

  readonly widgets = computed(() =>
    [...(this.dashboard()?.widgets ?? [])].sort(
      (a, b) => a.position - b.position,
    ),
  );

  readonly isEmpty = computed(
    () => this.state() === 'ready' && this.widgets().length === 0,
  );

  constructor() {
    // Warm the directories so widget-table GUIDs resolve to names (both are idempotent + reactive).
    this.userLookup.ensureLoaded();
    this.entityLookup.ensureLoaded();
    queueMicrotask(() => this.fetch());
  }

  fetch(): void {
    this.state.set('loading');
    this.service.getDashboard(this.idOrSlug()).subscribe({
      next: (detail) => {
        this.dashboard.set(detail);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  /* ---- Generic per-widget projections (memo-free; cheap on small payloads) ---- */

  keyFigures(widget: DashboardWidget): KeyFigure[] {
    return toKeyFigures(widget.data);
  }

  table(widget: DashboardWidget): TableProjection {
    return toTable(widget.data, {
      hidden: this.hiddenColumns,
      headerOverrides: this.headerOverrides,
    });
  }

  /** A `table` widget is a list — give it the full dashboard width, not a grid cell. */
  isWideWidget(widget: DashboardWidget): boolean {
    return widget.widgetType === 'table';
  }

  /**
   * Renders a widget-table cell: GUID columns resolve to a name (reactively, once the
   * directory loads), enum columns title-case, ISO dates shorten to the date, else plain text.
   */
  cellFor(column: string, value: unknown): string {
    if (typeof value === 'string' && value.length > 0) {
      const resolve = this.resolvers[column] ?? this.genericResolver(column);
      if (resolve) {
        return resolve(value);
      }
    }
    if (this.titleiseColumns.has(column) && typeof value === 'string') {
      return this.titleCase(value);
    }
    if (typeof value === 'string' && /^\d{4}-\d{2}-\d{2}T/.test(value)) {
      return value.slice(0, 10);
    }
    return cellText(value);
  }

  /**
   * Convention-based fallback for widget columns not in the explicit resolvers map: any `*UserId` / known `*By`
   * actor column → user name, `*EntityId` → entity name, `orgUnitId` → org-unit name. The lookups return the raw
   * value for anything they don't recognise, so applying this broadly is safe (a non-id value is returned unchanged).
   */
  private genericResolver(column: string): ((id: string) => string) | undefined {
    if (/UserId$/i.test(column) || this.userByColumns.has(column)) {
      return (id) => this.userLookup.displayName(id);
    }
    if (/EntityId$/i.test(column)) {
      return (id) => this.entityLookup.name(id);
    }
    if (/OrgUnitId$/i.test(column)) {
      return (id) => this.orgUnitLookup.name(id);
    }
    return undefined;
  }

  private titleCase(snake: string): string {
    return snake
      .split(/[_\s]+/)
      .map((w) => (w ? w.charAt(0).toUpperCase() + w.slice(1) : w))
      .join(' ');
  }

  bars(widget: DashboardWidget): BarDatum[] {
    return toBars(widget.data);
  }

  /** Bars/segments as chart data (BarDatum is a ChartDatum without a colour). */
  chartData(widget: DashboardWidget): ChartDatum[] {
    return toBars(widget.data);
  }

  /**
   * Picks the SVG chart kind for a `chart` widget from the data's shape. A
   * severity/status breakdown reads best as a donut (parts of a whole); any
   * other labelled series (age buckets, by-entity, coverage) as bars.
   */
  chartKind(widget: DashboardWidget): 'donut' | 'bar' | 'none' {
    const bars = toBars(widget.data);
    if (bars.length === 0) {
      return 'none';
    }
    return this.isBreakdown(widget.data) ? 'donut' : 'bar';
  }

  /** True when the payload exposes a `bySeverity` (parts-of-a-whole) array. */
  private isBreakdown(data: unknown): boolean {
    return (
      typeof data === 'object' &&
      data !== null &&
      Array.isArray((data as Record<string, unknown>)['bySeverity'])
    );
  }

  /**
   * A single percentage single-metric worth rendering as a gauge (plan
   * execution, closure/adherence rate) — returns `{ value, label }` or null so
   * the plain figure grid is used otherwise.
   */
  gaugeMetric(widget: DashboardWidget): { value: number; label: string } | null {
    const data = widget.data;
    if (typeof data !== 'object' || data === null) {
      return null;
    }
    const dict = data as Record<string, unknown>;
    const PREFERRED = [
      'planExecutionPercent',
      'closureRatePercent',
      'completionPercent',
      'overallGridAdherencePercent',
      'gridAdherencePercent',
    ];
    for (const key of PREFERRED) {
      const value = dict[key];
      if (typeof value === 'number' && Number.isFinite(value)) {
        return { value, label: this.gaugeLabel(key) };
      }
    }
    return null;
  }

  private gaugeLabel(key: string): string {
    const spaced = key
      .replace(/Percent$/, '')
      .replace(/([a-z0-9])([A-Z])/g, '$1 $2');
    return spaced.charAt(0).toUpperCase() + spaced.slice(1);
  }

  /** True when the widget's data payload is null/absent (degraded/forbidden). */
  isUnavailable(widget: DashboardWidget): boolean {
    return widget.data === null || widget.data === undefined;
  }

  /* ---- Admin widget config (ConfigureDashboards) ---- */

  addWidget(): void {
    const dash = this.dashboard();
    if (!dash || !this.canConfigure()) {
      return;
    }
    const nextPosition =
      Math.max(0, ...dash.widgets.map((w) => w.position)) + 1;
    this.dialog
      .open(WidgetDialogComponent, {
        width: '560px',
        data: { defaultPosition: nextPosition },
      })
      .afterClosed()
      .subscribe((result?: WidgetDialogResult) => {
        if (!result) {
          return;
        }
        this.service
          .addWidget(dash.id, { ...result, version: dash.version })
          .subscribe({
            next: (detail) => {
              this.dashboard.set(detail);
              this.notify.success(
                this.i18n.translate('analytics.view.widgetAdded', {
                  title: result.title,
                }),
              );
            },
            error: () =>
              this.notify.error(this.i18n.translate('analytics.view.addError')),
          });
      });
  }

  deleteWidget(widget: DashboardWidget): void {
    const dash = this.dashboard();
    if (!dash || !this.canConfigure()) {
      return;
    }
    const data: ConfirmDialogData = {
      title: this.i18n.translate('analytics.view.removeWidget'),
      message: this.i18n.translate('analytics.view.removeConfirm', {
        title: widget.title,
      }),
      confirmLabel: this.i18n.translate('analytics.view.removeConfirmLabel'),
      destructive: true,
    };
    this.dialog
      .open(ConfirmDialogComponent, { data, width: '420px' })
      .afterClosed()
      .subscribe((confirmed?: boolean) => {
        if (!confirmed) {
          return;
        }
        this.service
          .deleteWidget(dash.id, widget.id, { version: dash.version })
          .subscribe({
            next: (detail) => {
              this.dashboard.set(detail);
              this.notify.success(
                this.i18n.translate('analytics.view.widgetRemoved'),
              );
            },
            error: () =>
              this.notify.error(
                this.i18n.translate('analytics.view.removeError'),
              ),
          });
      });
  }
}
