import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { DatePipe, NgTemplateOutlet } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';

import { AuthService } from '../../core/services/auth.service';
import { AnalyticsService } from '../../core/services/analytics.service';
import { AuditsService } from '../../core/services/audits.service';
import { Permissions } from '../../core/permissions';
import { PageHeaderComponent } from '../../shared/components/page-header/page-header.component';
import {
  BarChartComponent,
  ChartDatum,
  DonutChartComponent,
  GaugeChartComponent,
} from '../../shared/charts';
import {
  ExceptionPortfolio,
  FunctionPerformance,
  MaterialFinding,
  PlanStatusKpi,
} from '../../core/models';
import { TranslatePipe } from '../../core/i18n/translate.pipe';
import { TranslationService } from '../../core/i18n/translation.service';

/** Accent palette key for a KPI card's icon chip. */
type Accent = 'indigo' | 'violet' | 'emerald' | 'amber' | 'rose' | 'sky';

interface Kpi {
  label: string;
  value: string;
  hint?: string;
  icon: string;
  accent: Accent;
  route?: string;
}

interface QuickLink {
  title: string;
  description: string;
  icon: string;
  route: string;
  accent: Accent;
  permissions: string[];
}

@Component({
  selector: 'app-dashboard',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    NgTemplateOutlet,
    RouterLink,
    MatCardModule,
    MatIconModule,
    MatButtonModule,
    MatButtonToggleModule,
    MatTooltipModule,
    PageHeaderComponent,
    BarChartComponent,
    DonutChartComponent,
    GaugeChartComponent,
    TranslatePipe,
  ],
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.scss',
})
export class DashboardComponent {
  private readonly auth = inject(AuthService);
  private readonly analytics = inject(AnalyticsService);
  private readonly audits = inject(AuditsService);
  private readonly i18n = inject(TranslationService);

  readonly session = this.auth.session;
  readonly firstName = computed(() => this.session()?.firstName ?? 'there');
  readonly roleCount = computed(() => this.session()?.roles.length ?? 0);
  readonly permissionCount = computed(
    () => this.session()?.permissions.length ?? 0,
  );

  /** Whether the signed-in user may see the audit-analytics surface. */
  readonly canSeeAnalytics = computed(() =>
    this.auth.hasPermission(Permissions.ViewAnalytics),
  );
  readonly canSeeAuditCounts = computed(() =>
    this.auth.hasAnyPermission(Permissions.ViewAudits, Permissions.ViewAnalytics),
  );

  /* ---- Live analytics (loaded only when permitted; null until/unless available) ---- */
  readonly performance = signal<FunctionPerformance | null>(null);
  readonly portfolio = signal<ExceptionPortfolio | null>(null);
  readonly planStatus = signal<PlanStatusKpi | null>(null);
  readonly auditCounts = signal<Record<string, number> | null>(null);
  readonly materialFindings = signal<MaterialFinding[]>([]);

  /* ---- Filters ---- */
  /** Look-back window (months) for the attention list; 0 = all time. */
  readonly windowMonths = signal(12);
  readonly severityFilter = signal<string>('all');

  constructor() {
    queueMicrotask(() => this.load());
  }

  private load(): void {
    if (this.canSeeAuditCounts()) {
      this.audits.counts().subscribe({
        next: (c) => this.auditCounts.set(c.byStatus),
        error: () => undefined,
      });
    }
    if (!this.canSeeAnalytics()) {
      return;
    }
    this.analytics.functionPerformance().subscribe({ next: (p) => this.performance.set(p), error: () => undefined });
    this.analytics.exceptionPortfolio().subscribe({ next: (p) => this.portfolio.set(p), error: () => undefined });
    this.analytics.planStatus().subscribe({ next: (p) => this.planStatus.set(p), error: () => undefined });
    this.analytics.materialFindings().subscribe({ next: (f) => this.materialFindings.set(f), error: () => undefined });
  }

  /* ---- KPI cards (adaptive to what the user can see) ---- */
  readonly kpis = computed<Kpi[]>(() => {
    const perf = this.performance();
    if (this.canSeeAnalytics() && perf) {
      return [
        {
          label: this.i18n.translate('dashboard.kpi.auditsInFlight'),
          value: `${perf.auditsInFlight}`,
          hint: this.i18n.translate('dashboard.kpi.auditsCompletedHint', { count: perf.auditsCompleted }),
          icon: 'pending_actions',
          accent: 'indigo',
          route: '/audits',
        },
        {
          label: this.i18n.translate('dashboard.kpi.planExecution'),
          value: `${Math.round(perf.planExecutionPercent)}%`,
          hint: this.i18n.translate('dashboard.kpi.planItemsHint', {
            done: perf.planItemsCompleted,
            total: perf.planItemsTotal,
          }),
          icon: 'donut_large',
          accent: 'violet',
          route: '/planning',
        },
        {
          label: this.i18n.translate('dashboard.kpi.openFindings'),
          value: `${perf.openExceptionBacklog}`,
          hint: this.i18n.translate('dashboard.kpi.closedFindingsHint', { count: perf.closedExceptions }),
          icon: 'report_problem',
          accent: 'rose',
          route: '/exceptions',
        },
        {
          label: this.i18n.translate('dashboard.kpi.closureRate'),
          value: `${Math.round(perf.closureRatePercent)}%`,
          hint: this.i18n.translate('dashboard.kpi.closureRateHint'),
          icon: 'task_alt',
          accent: 'emerald',
        },
      ];
    }

    // Governance fallback — session-derived, available to everyone.
    return [
      {
        label: this.i18n.translate('dashboard.stats.activeRoles'),
        value: `${this.roleCount()}`,
        icon: 'badge',
        accent: 'indigo',
      },
      {
        label: this.i18n.translate('dashboard.stats.permissionsGranted'),
        value: `${this.permissionCount()}`,
        icon: 'key',
        accent: 'violet',
      },
      {
        label: this.i18n.translate('dashboard.kpi.modulesAccessible'),
        value: `${this.moduleCount()}`,
        hint: this.i18n.translate('dashboard.kpi.modulesHint'),
        icon: 'apps',
        accent: 'sky',
      },
      {
        label: this.i18n.translate('dashboard.stats.sessionExpires'),
        value: this.sessionExpiryText(),
        icon: 'schedule',
        accent: 'amber',
      },
    ];
  });

  /* ---- Charts ---- */
  readonly auditStatusData = computed<ChartDatum[]>(() => {
    const counts = this.auditCounts();
    if (!counts) {
      return [];
    }
    return Object.entries(counts)
      .filter(([, v]) => v > 0)
      .map(([status, value]) => ({ label: this.humanise(status), value }));
  });

  readonly severityData = computed<ChartDatum[]>(() =>
    (this.portfolio()?.bySeverity ?? [])
      .filter((s) => s.count > 0)
      .map((s) => ({ label: s.severity, value: s.count })),
  );

  readonly ageData = computed<ChartDatum[]>(() =>
    (this.portfolio()?.byAgeBucket ?? []).map((b) => ({ label: b.bucket, value: b.count })),
  );

  /** Open findings by root-cause taxonomy (P2-A) — a pareto of causes. */
  readonly rootCauseData = computed<ChartDatum[]>(() =>
    (this.portfolio()?.byRootCause ?? [])
      .filter((r) => r.count > 0)
      .map((r) => ({ label: this.humanise(r.rootCauseCategory), value: r.count })),
  );

  readonly planPercent = computed(() => this.planStatus()?.completionPercent ?? null);

  /**
   * A donut of the user's granted permissions bucketed by access verb — the one
   * visual that is always available (derived from the in-memory session).
   */
  readonly accessProfile = computed<ChartDatum[]>(() => {
    const perms = this.session()?.permissions ?? [];
    const buckets: Record<string, number> = { View: 0, Manage: 0, Configure: 0, Approve: 0, Other: 0 };
    for (const p of perms) {
      const verb = ['View', 'Manage', 'Configure', 'Approve'].find((v) => p.startsWith(v));
      buckets[verb ?? 'Other'] += 1;
    }
    return Object.entries(buckets)
      .filter(([, count]) => count > 0)
      .map(([label, value]) => ({ label: this.i18n.translate(`dashboard.access.verb.${label}`), value }));
  });

  /* ---- Attention list (filterable) ---- */
  readonly severityOptions = ['all', 'critical', 'high'];

  readonly attentionFindings = computed<MaterialFinding[]>(() => {
    const months = this.windowMonths();
    const sev = this.severityFilter();
    const cutoff = months > 0 ? Date.now() - months * 30 * 24 * 3600 * 1000 : 0;
    return this.materialFindings()
      .filter((f) => (sev === 'all' ? true : f.severity === sev))
      .filter((f) => {
        if (months === 0) {
          return true;
        }
        const t = new Date(f.raisedAt).getTime();
        return Number.isNaN(t) || t >= cutoff;
      });
  });

  readonly hasCharts = computed(
    () =>
      this.auditStatusData().length > 0 ||
      this.severityData().length > 0 ||
      this.ageData().length > 0 ||
      this.planPercent() !== null,
  );

  setWindow(months: number): void {
    this.windowMonths.set(months);
  }

  setSeverity(sev: string): void {
    this.severityFilter.set(sev);
  }

  /* ---- Quick-launch grid ---- */
  private readonly allLinks: QuickLink[] = [
    { title: 'dashboard.launch.audits', description: 'dashboard.launch.auditsDesc', icon: 'assignment', route: '/audits', accent: 'indigo', permissions: [Permissions.ViewAudits] },
    { title: 'dashboard.launch.planning', description: 'dashboard.launch.planningDesc', icon: 'event_note', route: '/planning', accent: 'violet', permissions: [Permissions.ViewPlan] },
    { title: 'dashboard.launch.universe', description: 'dashboard.launch.universeDesc', icon: 'account_tree', route: '/audit-universe', accent: 'sky', permissions: [Permissions.ViewUniverse] },
    { title: 'dashboard.launch.exceptions', description: 'dashboard.launch.exceptionsDesc', icon: 'report_problem', route: '/exceptions', accent: 'rose', permissions: [Permissions.ViewExceptions] },
    { title: 'dashboard.launch.analytics', description: 'dashboard.launch.analyticsDesc', icon: 'insights', route: '/analytics', accent: 'emerald', permissions: [Permissions.ViewAnalytics] },
    { title: 'dashboard.launch.templates', description: 'dashboard.launch.templatesDesc', icon: 'checklist', route: '/admin/templates', accent: 'amber', permissions: [Permissions.ViewTemplates] },
    { title: 'dashboard.card.users.title', description: 'dashboard.card.users.desc', icon: 'group', route: '/admin/users', accent: 'sky', permissions: [Permissions.ManageUsers] },
    { title: 'dashboard.card.roles.title', description: 'dashboard.card.roles.desc', icon: 'admin_panel_settings', route: '/admin/roles', accent: 'indigo', permissions: [Permissions.ManageRoles] },
  ];

  readonly quickLinks = computed(() =>
    this.allLinks.filter(
      (c) => c.permissions.length === 0 || this.auth.hasAnyPermission(...c.permissions),
    ),
  );

  /* ---- helpers ---- */
  private moduleCount(): number {
    // The number of first-class workspaces the user can open from the launchpad.
    return this.quickLinks().length;
  }

  private sessionExpiryText(): string {
    const exp = this.sessionExpiry();
    if (!exp) {
      return '—';
    }
    return exp.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  }

  private humanise(snake: string): string {
    return snake.replace(/_/g, ' ').replace(/\b\w/g, (c) => c.toUpperCase());
  }

  readonly sessionExpiry = computed(() => {
    const expiresAt = this.session()?.expiresAt;
    if (!expiresAt) {
      return null;
    }
    const date = new Date(expiresAt);
    return Number.isNaN(date.getTime()) ? null : date;
  });
}
