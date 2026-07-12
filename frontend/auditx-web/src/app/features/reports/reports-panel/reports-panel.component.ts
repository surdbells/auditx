import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  input,
  signal,
} from '@angular/core';
import { DatePipe, UpperCasePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpResponse } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { IconComponent } from '../../../core/icons/icon.component';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import { Subscription, timer } from 'rxjs';

import { ReportsService } from '../../../core/services/reports.service';
import { AuditsService } from '../../../core/services/audits.service';
import { NotificationService } from '../../../core/services/notification.service';
import { AuthService } from '../../../core/services/auth.service';
import { Permissions } from '../../../core/permissions';
import { AuditStatus, Report, ReportListItem } from '../../../core/models';
import { humanise, shortHash } from '../humanise';
import { downloadBlobResponse } from '../download';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { PaginatorComponent } from '../../../shared/components/paginator/paginator.component';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';

type ViewState = 'loading' | 'ready' | 'error';

/** Audit statuses from which a report may be generated. */
const GENERATABLE: AuditStatus[] = ['under_review', 'completed'];

/** Poll interval (ms) while a generation is pending/running. */
const POLL_INTERVAL = 2000;

const DEFAULT_PAGE_SIZE = 25;

@Component({
  selector: 'app-reports-panel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    UpperCasePipe,
    FormsModule,
    RouterLink,
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    MatCheckboxModule,
    IconComponent,
    MatProgressSpinnerModule,
    MatTooltipModule,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PageHeaderComponent,
    PaginatorComponent,
    TranslatePipe,
  ],
  templateUrl: './reports-panel.component.html',
  styleUrl: './reports-panel.component.scss',
})
export class ReportsPanelComponent {
  /** Route param bound via withComponentInputBinding. */
  readonly auditId = input.required<string>();

  private readonly service = inject(ReportsService);
  private readonly audits = inject(AuditsService);
  private readonly notify = inject(NotificationService);
  private readonly auth = inject(AuthService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly i18n = inject(TranslationService);

  readonly displayedColumns = [
    'version',
    'status',
    'requestedAt',
    'hash',
    'actions',
  ];

  readonly state = signal<ViewState>('loading');
  readonly reports = signal<ReportListItem[]>([]);
  readonly total = signal(0);
  readonly page = signal(1);
  readonly pageSize = signal(DEFAULT_PAGE_SIZE);
  /** In-flight fetch (page navigation / filter change) — disables the paginator without clearing the table. */
  readonly loading = signal(false);
  readonly auditStatus = signal<AuditStatus | null>(null);
  readonly auditName = signal<string>('');
  /** Set while a generation is pending/running and being polled. */
  readonly generating = signal(false);
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

  /** Generate is offered only in under_review / completed audit states. */
  readonly canGenerateNow = computed(() => {
    const s = this.auditStatus();
    return this.canGenerate() && s !== null && GENERATABLE.includes(s);
  });

  readonly isEmpty = computed(
    () => this.state() === 'ready' && this.reports().length === 0,
  );

  constructor() {
    queueMicrotask(() => this.fetch());
    this.destroyRef.onDestroy(() => this.pollSub?.unsubscribe());
  }

  fetch(): void {
    this.state.set('loading');
    this.audits.getById(this.auditId()).subscribe({
      next: (audit) => {
        this.auditStatus.set(audit.status);
        this.auditName.set(audit.name);
        this.fetchPage(1);
      },
      error: () => this.state.set('error'),
    });
  }

  fetchPage(page: number): void {
    this.loading.set(true);
    this.service.listForAudit(this.auditId(), page, this.pageSize()).subscribe({
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
    if (!this.canGenerateNow() || this.generating()) {
      return;
    }
    this.generating.set(true);
    this.service
      .generate(this.auditId(), { docx: this.includeDocx })
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
          this.notify.error(
            this.i18n.translate('reports.panel.notify.lostTrack'),
          );
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

  private handleDownload(
    res: HttpResponse<Blob>,
    row: ReportListItem,
    format: string,
  ): void {
    const fallback = `report-v${row.versionNumber}.${format}`;
    if (!downloadBlobResponse(res, fallback)) {
      this.notify.error(
        this.i18n.translate('reports.common.notify.downloadEmpty'),
      );
    }
  }
}
