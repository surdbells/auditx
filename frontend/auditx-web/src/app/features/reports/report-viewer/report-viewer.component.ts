import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  signal,
} from '@angular/core';
import { DatePipe, UpperCasePipe } from '@angular/common';
import { HttpResponse } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';

import { ReportsService } from '../../../core/services/reports.service';
import { AuditsService } from '../../../core/services/audits.service';
import { UsersService } from '../../../core/services/users.service';
import { NotificationService } from '../../../core/services/notification.service';
import { AuthService } from '../../../core/services/auth.service';
import { Permissions } from '../../../core/permissions';
import {
  Report,
  ReportArtefact,
  ReportDistribution,
  ReportHashVerification,
  UserDto,
} from '../../../core/models';
import { humanise, shortHash } from '../humanise';
import { downloadBlobResponse } from '../download';
import { printReportHtml } from '../print';
import {
  DistributeReportDialogComponent,
  DistributeReportDialogData,
} from '../dialogs/distribute-report-dialog.component';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';

type ViewState = 'loading' | 'ready' | 'error';

const DISTRIBUTIONS_PAGE_SIZE = 7;

@Component({
  selector: 'app-report-viewer',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    UpperCasePipe,
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    LoadingComponent,
    ErrorStateComponent,
    PageHeaderComponent,
    TranslatePipe,
  ],
  templateUrl: './report-viewer.component.html',
  styleUrl: './report-viewer.component.scss',
})
export class ReportViewerComponent {
  /** Route param bound via withComponentInputBinding. */
  readonly id = input.required<string>();

  private readonly service = inject(ReportsService);
  private readonly audits = inject(AuditsService);
  private readonly users = inject(UsersService);
  private readonly notify = inject(NotificationService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly i18n = inject(TranslationService);

  readonly distributionColumns = [
    'recipient',
    'version',
    'dispatchedAt',
    'outcome',
  ];

  readonly state = signal<ViewState>('loading');
  readonly report = signal<Report | null>(null);
  /** Resolved audit name for the header subtitle; falls back to the id. */
  readonly auditName = signal<string | null>(null);

  readonly distributions = signal<ReportDistribution[]>([]);
  readonly nextCursor = signal<string | null>(null);
  readonly hasMore = signal(false);
  readonly loadingMore = signal(false);

  readonly verification = signal<ReportHashVerification | null>(null);
  readonly verifying = signal(false);
  readonly printing = signal(false);

  private usersCache: UserDto[] = [];

  readonly humanise = humanise;
  readonly shortHash = shortHash;

  readonly canView = computed(() =>
    this.auth.hasPermission(Permissions.ViewReport),
  );
  readonly canDistribute = computed(() =>
    this.auth.hasPermission(Permissions.DistributeReport),
  );

  readonly status = computed(() => this.report()?.status ?? null);
  readonly isCompleted = computed(() => this.status() === 'completed');

  readonly artefacts = computed<ReportArtefact[]>(
    () => this.report()?.producedArtefacts ?? [],
  );

  constructor() {
    queueMicrotask(() => this.fetch());
  }

  fetch(): void {
    this.state.set('loading');
    this.service.getById(this.id()).subscribe({
      next: (report) => {
        this.report.set(report);
        this.state.set('ready');
        this.resolveAuditName(report.auditId);
        this.ensureUsers(() => this.loadDistributions());
      },
      error: () => this.state.set('error'),
    });
  }

  /** Resolves the audit name for the subtitle; leaves null (→ id) on failure. */
  private resolveAuditName(auditId: string): void {
    this.audits.getById(auditId).subscribe({
      next: (audit) => this.auditName.set(audit.name),
      error: () => {
        // Non-fatal: the header falls back to the audit id.
      },
    });
  }

  /* ---- Download ---- */

  download(artefact: ReportArtefact): void {
    this.service.download(this.id(), artefact.format).subscribe({
      next: (res) => this.handleDownload(res, artefact.format),
      error: () =>
        this.notify.error(
          this.i18n.translate('reports.common.notify.downloadError'),
        ),
    });
  }

  private handleDownload(res: HttpResponse<Blob>, format: string): void {
    const version = this.report()?.versionNumber ?? 0;
    const fallback = `report-v${version}.${format}`;
    if (!downloadBlobResponse(res, fallback)) {
      this.notify.error(
        this.i18n.translate('reports.common.notify.downloadEmpty'),
      );
    }
  }

  /* ---- Print / Save as PDF ---- */

  /** Prints the canonical HTML artefact (exact bytes) via the browser dialog — the user can Save as PDF. */
  printReport(): void {
    if (this.printing()) {
      return;
    }
    this.printing.set(true);
    this.service.download(this.id(), 'html').subscribe({
      next: async (res) => {
        this.printing.set(false);
        const blob = res.body;
        if (!blob) {
          this.notify.error(this.i18n.translate('reports.common.notify.downloadEmpty'));
          return;
        }
        printReportHtml(await blob.text());
      },
      error: () => {
        this.printing.set(false);
        this.notify.error(this.i18n.translate('reports.common.notify.downloadError'));
      },
    });
  }

  /* ---- Verify hash ---- */

  verifyHash(): void {
    if (this.verifying()) {
      return;
    }
    this.verifying.set(true);
    this.service.verifyHash(this.id()).subscribe({
      next: (result) => {
        this.verifying.set(false);
        this.verification.set(result);
      },
      error: () => {
        this.verifying.set(false);
        this.notify.error(
          this.i18n.translate('reports.viewer.notify.verifyError'),
        );
      },
    });
  }

  /* ---- Distribution ---- */

  distribute(): void {
    this.ensureUsers(() => {
      const data: DistributeReportDialogData = { users: this.usersCache };
      this.dialog
        .open(DistributeReportDialogComponent, { data, width: '560px' })
        .afterClosed()
        .subscribe((result) => {
          if (!result) {
            return;
          }
          this.service.distribute(this.id(), result).subscribe({
            next: (res) => {
              this.notify.success(
                this.i18n.translate('reports.viewer.notify.distributed', {
                  count: res.recipientCount,
                }),
              );
              this.reloadDistributions();
            },
            error: () =>
              this.notify.error(
                this.i18n.translate('reports.viewer.notify.distributeError'),
              ),
          });
        });
    });
  }

  private reloadDistributions(): void {
    this.distributions.set([]);
    this.nextCursor.set(null);
    this.hasMore.set(false);
    this.loadDistributions();
  }

  private loadDistributions(): void {
    this.service.distributions(this.id(), null, DISTRIBUTIONS_PAGE_SIZE).subscribe({
      next: (page) => {
        this.distributions.set(page.items);
        this.nextCursor.set(page.nextCursor);
        this.hasMore.set(page.hasMore);
      },
      error: () => {
        // Non-fatal: leave the log empty.
      },
    });
  }

  loadMore(): void {
    if (!this.hasMore() || this.loadingMore()) {
      return;
    }
    this.loadingMore.set(true);
    this.service
      .distributions(this.id(), this.nextCursor(), DISTRIBUTIONS_PAGE_SIZE)
      .subscribe({
        next: (page) => {
          this.distributions.update((current) => [...current, ...page.items]);
          this.nextCursor.set(page.nextCursor);
          this.hasMore.set(page.hasMore);
          this.loadingMore.set(false);
        },
        error: () => this.loadingMore.set(false),
      });
  }

  /* ---- Recipient name resolution ---- */

  recipientLabel(row: ReportDistribution): string {
    if (row.recipientEmail) {
      return row.recipientEmail;
    }
    if (row.recipientUserId) {
      return this.nameOf(row.recipientUserId);
    }
    return '—';
  }

  private nameOf(userId: string): string {
    return this.usersCache.find((u) => u.id === userId)?.displayName ?? userId;
  }

  private ensureUsers(onReady: () => void): void {
    if (this.usersCache.length) {
      onReady();
      return;
    }
    this.users.list({ status: 'active', limit: 200 }).subscribe({
      next: (page) => {
        this.usersCache = page.items;
        onReady();
      },
      error: () => onReady(),
    });
  }
}
