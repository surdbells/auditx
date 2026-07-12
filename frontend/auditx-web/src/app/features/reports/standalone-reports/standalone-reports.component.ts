import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  signal,
} from '@angular/core';
import { DatePipe, UpperCasePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpResponse } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { IconComponent } from '../../../core/icons/icon.component';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import { Subscription, timer } from 'rxjs';

import { ReportsService } from '../../../core/services/reports.service';
import { NotificationService } from '../../../core/services/notification.service';
import { AuthService } from '../../../core/services/auth.service';
import { Permissions } from '../../../core/permissions';
import {
  Report,
  ReportListItem,
  StandaloneReportKind,
} from '../../../core/models';
import { humanise, shortHash } from '../humanise';
import { downloadBlobResponse } from '../download';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { PaginatorComponent } from '../../../shared/components/paginator/paginator.component';
import { PageGuideComponent } from '../../../shared/components/page-guide/page-guide.component';
import { PageGuide } from '../../../core/models/page-guide.models';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';

type ViewState = 'loading' | 'ready' | 'error';

/** Poll interval (ms) while a generation is pending/running. */
const POLL_INTERVAL = 2000;

const DEFAULT_PAGE_SIZE = 25;

/** The selectable standalone report kinds, with their nav/label metadata. */
interface KindOption {
  value: StandaloneReportKind;
  labelKey: string;
  icon: string;
}

const KIND_OPTIONS: KindOption[] = [
  { value: 'executive_summary', labelKey: 'reports.standalone.kind.executiveSummary', icon: 'summarize' },
  { value: 'annual_plan_status', labelKey: 'reports.standalone.kind.annualPlanStatus', icon: 'event_note' },
  { value: 'kpi_pack', labelKey: 'reports.standalone.kind.kpiPack', icon: 'insights' },
  { value: 'audit_coverage', labelKey: 'reports.standalone.kind.auditCoverage', icon: 'grid_view' },
  { value: 'findings_register', labelKey: 'reports.standalone.kind.findingsRegister', icon: 'report_problem' },
  { value: 'sanctions_consistency', labelKey: 'reports.standalone.kind.sanctionsConsistency', icon: 'gavel' },
  { value: 'performance_scorecards', labelKey: 'reports.standalone.kind.performanceScorecards', icon: 'leaderboard' },
];

/** Kinds whose CONTENT is gated by an extra permission beyond ViewAnalytics. */
const RESTRICTED_KINDS: Record<string, string> = {
  performance_scorecards: Permissions.PerformanceAnalyticsView,
};

/** Contextual page guide for the standalone (cross-audit) reports page — drives the walkthrough + About panel. */
const STANDALONE_REPORTS_GUIDE: PageGuide = {
  id: 'standalone-reports',
  titleKey: 'reports.standalone.title',
  purposeKey: 'reports.guide.purpose',
  descriptionKey: 'reports.guide.description',
  actionKeys: [
    'reports.guide.action.generate',
    'reports.guide.action.filter',
    'reports.guide.action.open',
    'reports.guide.action.download',
  ],
  sections: [
    { selector: '.std__generate', titleKey: 'reports.guide.section.generate.title', bodyKey: 'reports.guide.section.generate.body' },
    { selector: '.std__filters', titleKey: 'reports.guide.section.filters.title', bodyKey: 'reports.guide.section.filters.body' },
    { selector: '.std__table', titleKey: 'reports.guide.section.table.title', bodyKey: 'reports.guide.section.table.body' },
  ],
  workflowKeys: [
    'reports.guide.flow.capture',
    'reports.guide.flow.analytics',
    'reports.guide.flow.generate',
    'reports.guide.flow.review',
    'reports.guide.flow.distribute',
  ],
  dependsOnKeys: [
    'reports.guide.dep.analytics',
    'reports.guide.dep.audits',
    'reports.guide.dep.findings',
    'reports.guide.dep.sanctions',
  ],
  usedByKeys: [
    'reports.guide.use.stakeholders',
    'reports.guide.use.schedules',
    'reports.guide.use.audit',
  ],
  businessRuleKeys: [
    'reports.guide.rule.versioned',
    'reports.guide.rule.async',
    'reports.guide.rule.hash',
    'reports.guide.rule.restricted',
  ],
  tipKeys: [
    'reports.guide.tip.docx',
    'reports.guide.tip.filter',
    'reports.guide.tip.hash',
  ],
  permissionKeys: [
    'reports.guide.perm.generate',
    'reports.guide.perm.view',
    'reports.guide.perm.restricted',
  ],
  faq: [
    { questionKey: 'reports.guide.faq.kinds.q', answerKey: 'reports.guide.faq.kinds.a' },
    { questionKey: 'reports.guide.faq.pending.q', answerKey: 'reports.guide.faq.pending.a' },
  ],
};

@Component({
  selector: 'app-standalone-reports',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    UpperCasePipe,
    FormsModule,
    RouterLink,
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    MatButtonToggleModule,
    MatCheckboxModule,
    MatFormFieldModule,
    MatSelectModule,
    IconComponent,
    MatProgressSpinnerModule,
    MatTooltipModule,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PageHeaderComponent,
    PaginatorComponent,
    PageGuideComponent,
    TranslatePipe,
  ],
  templateUrl: './standalone-reports.component.html',
  styleUrl: './standalone-reports.component.scss',
})
export class StandaloneReportsComponent {
  private readonly service = inject(ReportsService);
  private readonly notify = inject(NotificationService);
  private readonly auth = inject(AuthService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly i18n = inject(TranslationService);

  readonly kindOptions = KIND_OPTIONS;
  readonly guide = STANDALONE_REPORTS_GUIDE;
  readonly displayedColumns = ['kind', 'version', 'status', 'requestedAt', 'hash', 'actions'];

  readonly state = signal<ViewState>('loading');
  readonly reports = signal<ReportListItem[]>([]);
  readonly total = signal(0);
  readonly page = signal(1);
  readonly pageSize = signal(DEFAULT_PAGE_SIZE);
  /** In-flight fetch (page navigation / filter change) — disables the paginator without clearing the table. */
  readonly loading = signal(false);
  /** Set while a generation is pending/running and being polled. */
  readonly generating = signal(false);

  /** The kind chosen in the generate control. */
  readonly newKind = signal<StandaloneReportKind>('executive_summary');
  /** The list filter (null = all kinds). */
  readonly filterKind = signal<StandaloneReportKind | null>(null);
  /** DOCX opt-in for the next generation, bound to the checkbox. */
  includeDocx = false;

  private pollSub: Subscription | null = null;

  readonly humanise = humanise;
  readonly shortHash = shortHash;

  readonly canGenerate = computed(() =>
    this.auth.hasPermission(Permissions.GenerateReport),
  );
  readonly canView = computed(() =>
    this.auth.hasPermission(Permissions.ViewReport),
  );

  /** Kinds the caller may generate — restricted kinds (e.g. scorecards) drop out without the extra permission. */
  readonly generateKinds = computed(() =>
    this.kindOptions.filter((o) => this.canOpenKind(o.value)),
  );

  /** True when the caller can open/download a report of this kind (restricted kinds need an extra permission). */
  canOpenKind(kind: string): boolean {
    const required = RESTRICTED_KINDS[kind];
    return !required || this.auth.hasPermission(required);
  }

  readonly isEmpty = computed(
    () => this.state() === 'ready' && this.reports().length === 0,
  );

  constructor() {
    queueMicrotask(() => this.fetchPage(1));
    this.destroyRef.onDestroy(() => this.pollSub?.unsubscribe());
  }

  /** Re-filter the list to a single kind, or all kinds when value is null. */
  applyFilter(kind: StandaloneReportKind | null): void {
    this.filterKind.set(kind);
    this.fetchPage(1);
  }

  fetchPage(page: number): void {
    this.loading.set(true);
    this.service.listStandalone(this.filterKind(), page, this.pageSize()).subscribe({
      next: (result) => {
        this.reports.set(result.items);
        this.total.set(result.total);
        this.page.set(result.page);
        this.state.set('ready');
        this.loading.set(false);
      },
      error: () => {
        if (this.state() === 'loading') {
          this.state.set('error');
        }
        this.loading.set(false);
      },
    });
  }

  onPageChange(page: number): void {
    this.fetchPage(page);
  }

  onPageSizeChange(size: number): void {
    this.pageSize.set(size);
    this.fetchPage(1);
  }

  generate(): void {
    if (!this.canGenerate() || this.generating()) {
      return;
    }
    this.generating.set(true);
    this.service
      .generateStandalone({ kind: this.newKind(), docx: this.includeDocx })
      .subscribe({
        next: (result) => {
          this.notify.info(
            this.i18n.translate('reports.panel.notify.generationStarted'),
          );
          this.pollUntilSettled(result.reportId);
        },
        error: () => {
          this.generating.set(false);
          this.notify.error(
            this.i18n.translate('reports.panel.notify.generateError'),
          );
        },
      });
  }

  /** Polls GET /reports/{id} every ~2s until status is completed/failed. */
  private pollUntilSettled(reportId: string): void {
    this.pollSub?.unsubscribe();
    this.pollSub = timer(0, POLL_INTERVAL).subscribe(() => {
      this.service.getById(reportId).subscribe({
        next: (report) => this.onPoll(report),
        error: () => {
          this.pollSub?.unsubscribe();
          this.generating.set(false);
          this.notify.error(this.i18n.translate('reports.panel.notify.lostTrack'));
          this.fetchPage(1);
        },
      });
    });
  }

  private onPoll(report: Report): void {
    if (report.status === 'completed') {
      this.pollSub?.unsubscribe();
      this.generating.set(false);
      this.notify.success(
        this.i18n.translate('reports.panel.notify.ready', {
          version: report.versionNumber,
        }),
      );
      this.fetchPage(1);
    } else if (report.status === 'failed') {
      this.pollSub?.unsubscribe();
      this.generating.set(false);
      this.notify.error(
        report.failureReason ||
          this.i18n.translate('reports.panel.notify.generationFailed'),
      );
      this.fetchPage(1);
    }
    // pending / running: keep polling.
  }

  download(row: ReportListItem, format: string): void {
    this.service.download(row.id, format).subscribe({
      next: (res) => this.handleDownload(res, row, format),
      error: () =>
        this.notify.error(
          this.i18n.translate('reports.common.notify.downloadError'),
        ),
    });
  }

  private handleDownload(res: HttpResponse<Blob>, row: ReportListItem, format: string): void {
    const fallback = `${row.kind}-v${row.versionNumber}.${format}`;
    if (!downloadBlobResponse(res, fallback)) {
      this.notify.error(
        this.i18n.translate('reports.common.notify.downloadEmpty'),
      );
    }
  }
}
