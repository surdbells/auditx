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

import { AnalyticsService } from '../../../core/services/analytics.service';
import { NotificationService } from '../../../core/services/notification.service';
import { AuthService } from '../../../core/services/auth.service';
import { Permissions } from '../../../core/permissions';
import { DashboardDetail, DashboardWidget } from '../../../core/models';
import {
  BarDatum,
  KeyFigure,
  TableProjection,
  cellText,
  maxBar,
  toBars,
  toKeyFigures,
  toTable,
} from '../widget-data';
import { humanise } from '../format';
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
 * degrades a forbidden widget to null). Charts use inline CSS bars — no
 * charting dependency is added.
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
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PageHeaderComponent,
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
    return toTable(widget.data);
  }

  bars(widget: DashboardWidget): BarDatum[] {
    return toBars(widget.data);
  }

  barWidth(value: number, bars: BarDatum[]): string {
    return `${Math.round((value / maxBar(bars)) * 100)}%`;
  }

  cell(value: unknown): string {
    return cellText(value);
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
              this.notify.success(`Widget "${result.title}" added.`);
            },
            error: () => this.notify.error('We could not add the widget.'),
          });
      });
  }

  deleteWidget(widget: DashboardWidget): void {
    const dash = this.dashboard();
    if (!dash || !this.canConfigure()) {
      return;
    }
    const data: ConfirmDialogData = {
      title: 'Remove widget',
      message: `Remove "${widget.title}" from this dashboard?`,
      confirmLabel: 'Remove',
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
              this.notify.success('Widget removed.');
            },
            error: () => this.notify.error('We could not remove the widget.'),
          });
      });
  }
}
