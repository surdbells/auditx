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
import { MatIconModule } from '@angular/material/icon';
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
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';

type ViewState = 'loading' | 'ready' | 'error';

/** Poll interval (ms) while a generation is pending/running. */
const POLL_INTERVAL = 2000;

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
];

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
    MatIconModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PageHeaderComponent,
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
  readonly displayedColumns = ['kind', 'version', 'status', 'requestedAt', 'hash', 'actions'];

  readonly state = signal<ViewState>('loading');
  readonly reports = signal<ReportListItem[]>([]);
  readonly nextCursor = signal<string | null>(null);
  readonly hasMore = signal(false);
  readonly loadingMore = signal(false);
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

  readonly isEmpty = computed(
    () => this.state() === 'ready' && this.reports().length === 0,
  );

  constructor() {
    queueMicrotask(() => this.loadList());
    this.destroyRef.onDestroy(() => this.pollSub?.unsubscribe());
  }

  /** Re-filter the list to a single kind, or all kinds when value is null. */
  applyFilter(kind: StandaloneReportKind | null): void {
    this.filterKind.set(kind);
    this.loadList();
  }

  loadList(): void {
    this.state.set('loading');
    this.nextCursor.set(null);
    this.service.listStandalone(this.filterKind()).subscribe({
      next: (page) => {
        this.reports.set(page.items);
        this.nextCursor.set(page.nextCursor);
        this.hasMore.set(page.hasMore);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  loadMore(): void {
    if (!this.hasMore() || this.loadingMore()) {
      return;
    }
    this.loadingMore.set(true);
    this.service.listStandalone(this.filterKind(), this.nextCursor()).subscribe({
      next: (page) => {
        this.reports.update((current) => [...current, ...page.items]);
        this.nextCursor.set(page.nextCursor);
        this.hasMore.set(page.hasMore);
        this.loadingMore.set(false);
      },
      error: () => this.loadingMore.set(false),
    });
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
          this.loadList();
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
      this.loadList();
    } else if (report.status === 'failed') {
      this.pollSub?.unsubscribe();
      this.generating.set(false);
      this.notify.error(
        report.failureReason ||
          this.i18n.translate('reports.panel.notify.generationFailed'),
      );
      this.loadList();
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
