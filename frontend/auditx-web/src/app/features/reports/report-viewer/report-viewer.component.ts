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
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { IconComponent } from '../../../core/icons/icon.component';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';

import { ReportsService } from '../../../core/services/reports.service';
import { AuditsService } from '../../../core/services/audits.service';
import { UsersService } from '../../../core/services/users.service';
import { UserLookupService } from '../../../core/services/user-lookup.service';
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
import { ReportShareDialogComponent } from '../dialogs/report-share-dialog.component';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { PageGuideComponent } from '../../../shared/components/page-guide/page-guide.component';
import { PageGuide } from '../../../core/models/page-guide.models';
import { PaginatorComponent } from '../../../shared/components/paginator/paginator.component';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';

type ViewState = 'loading' | 'ready' | 'error';

const DEFAULT_PAGE_SIZE = 25;

/** Contextual page guide for the report viewer (drives the walkthrough + the About panel). */
const REPORT_VIEWER_GUIDE: PageGuide = {
  id: 'report-viewer',
  titleKey: 'reports.viewer.guideTitle',
  purposeKey: 'reports.viewer.guide.purpose',
  descriptionKey: 'reports.viewer.guide.description',
  actionKeys: [
    'reports.viewer.guide.action.preview',
    'reports.viewer.guide.action.verify',
    'reports.viewer.guide.action.download',
    'reports.viewer.guide.action.distribute',
    'reports.viewer.guide.action.share',
  ],
  sections: [
    { selector: '.viewer__card', titleKey: 'reports.viewer.guide.section.overview.title', bodyKey: 'reports.viewer.guide.section.overview.body' },
    { selector: '.viewer__table-card', titleKey: 'reports.viewer.guide.section.distribution.title', bodyKey: 'reports.viewer.guide.section.distribution.body' },
  ],
  dependsOnKeys: ['reports.viewer.guide.dep.panel'],
  usedByKeys: ['reports.viewer.guide.use.ac', 'reports.viewer.guide.use.sharedLinks'],
  businessRuleKeys: ['reports.viewer.guide.rule.sealed', 'reports.viewer.guide.rule.pending'],
  tipKeys: ['reports.viewer.guide.tip.verify'],
  permissionKeys: ['reports.viewer.guide.perm.view', 'reports.viewer.guide.perm.distribute'],
  faq: [
    { questionKey: 'reports.viewer.guide.faq.hash.q', answerKey: 'reports.viewer.guide.faq.hash.a' },
  ],
};

@Component({
  selector: 'app-report-viewer',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    UpperCasePipe,
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    IconComponent,
    MatProgressSpinnerModule,
    MatTooltipModule,
    LoadingComponent,
    ErrorStateComponent,
    PageHeaderComponent,
    PageGuideComponent,
    PaginatorComponent,
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
  /** Active users for the distribute-report recipient picker (assignable = active). */
  private readonly users = inject(UsersService);
  /** Directory-backed user-name resolver for the distribution log (all users, no admin permission). */
  private readonly userLookup = inject(UserLookupService);
  private readonly notify = inject(NotificationService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly i18n = inject(TranslationService);
  private readonly sanitizer = inject(DomSanitizer);

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
  readonly total = signal(0);
  readonly page = signal(1);
  readonly pageSize = signal(DEFAULT_PAGE_SIZE);
  /** In-flight distribution-log fetch — disables the paginator without clearing the table. */
  readonly loading = signal(false);

  readonly verification = signal<ReportHashVerification | null>(null);
  readonly verifying = signal(false);
  readonly printing = signal(false);

  /** Inline preview of the canonical HTML artefact (view before export). */
  readonly previewHtml = signal<SafeHtml | null>(null);
  readonly previewState = signal<'idle' | 'loading' | 'ready' | 'error'>('idle');

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

  readonly guide = REPORT_VIEWER_GUIDE;

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
        this.loadPreview(report);
        this.ensureUsers(() => this.fetchDistributions(1));
      },
      error: () => this.state.set('error'),
    });
  }

  /**
   * Loads the canonical HTML artefact and renders it inline (view before export). The report HTML is server-rendered
   * with every dynamic value HTML-encoded and carries no scripts; it is shown in a fully-sandboxed iframe.
   */
  private loadPreview(report: Report): void {
    if (report.status !== 'completed') {
      this.previewState.set('idle');
      return;
    }
    this.previewState.set('loading');
    this.service.download(this.id(), 'html').subscribe({
      next: async (res) => {
        const blob = res.body;
        if (!blob) {
          this.previewState.set('error');
          return;
        }
        this.previewHtml.set(this.sanitizer.bypassSecurityTrustHtml(await blob.text()));
        this.previewState.set('ready');
      },
      error: () => this.previewState.set('error'),
    });
  }

  /**
   * Resolves the audit name for the subtitle; leaves null (→ id) on failure. Standalone (cross-audit) reports have
   * no audit — the subtitle falls back to the report kind (see {@link subtitle}).
   */
  private resolveAuditName(auditId: string | null): void {
    if (!auditId) {
      return;
    }
    this.audits.getById(auditId).subscribe({
      next: (audit) => this.auditName.set(audit.name),
      error: () => {
        // Non-fatal: the header falls back to the audit id.
      },
    });
  }

  /** Header subtitle: the audit for engagement reports, or the humanised kind for standalone reports. */
  readonly subtitle = computed(() => {
    const r = this.report();
    if (!r) {
      return '';
    }
    if (r.auditId) {
      return this.i18n.translate('reports.viewer.subtitle', {
        audit: this.auditName() ?? r.auditId,
      });
    }
    return humanise(r.kind);
  });

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

  /* ---- Share links (D3-B) ---- */

  /** Opens the share dialog to create / copy / revoke shareable links for this report. */
  share(): void {
    this.dialog.open(ReportShareDialogComponent, {
      data: { reportId: this.id() },
      width: '560px',
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
              this.fetchDistributions(1);
            },
            error: () =>
              this.notify.error(
                this.i18n.translate('reports.viewer.notify.distributeError'),
              ),
          });
        });
    });
  }

  fetchDistributions(page: number): void {
    this.loading.set(true);
    this.service.distributions(this.id(), page, this.pageSize()).subscribe({
      next: (result) => {
        this.distributions.set(result.items);
        this.total.set(result.total);
        this.page.set(result.page);
        this.loading.set(false);
      },
      error: () => {
        // Non-fatal: leave the log as-is.
        this.loading.set(false);
      },
    });
  }

  onPageChange(page: number): void {
    this.fetchDistributions(page);
  }

  onPageSizeChange(size: number): void {
    this.pageSize.set(size);
    this.fetchDistributions(1);
  }

  /* ---- Delivery-outcome confirmation ---- */

  /** The distribution whose outcome is being confirmed (disables its buttons while in flight). */
  readonly confirmingOutcomeId = signal<string | null>(null);

  /** Confirm a pending distribution's outcome (delivered/bounced). Final once recorded. */
  confirmOutcome(row: ReportDistribution, outcome: 'delivered' | 'bounced'): void {
    this.confirmingOutcomeId.set(row.id);
    this.service.recordDistributionOutcome(this.id(), row.id, outcome).subscribe({
      next: (updated) => {
        // Patch the row in place so the badge flips without losing the current page.
        this.distributions.update((rows) => rows.map((d) => (d.id === updated.id ? updated : d)));
        this.notify.success(this.i18n.translate('reports.viewer.outcome.recorded'));
        this.confirmingOutcomeId.set(null);
      },
      error: () => this.confirmingOutcomeId.set(null),
    });
  }

  /* ---- Recipient name resolution ---- */

  recipientLabel(row: ReportDistribution): string {
    if (row.recipientEmail) {
      return row.recipientEmail;
    }
    if (row.recipientUserId) {
      // Directory-backed: resolves any recipient (incl. since-deactivated users), unlike the active-only picker cache.
      return this.userLookup.displayName(row.recipientUserId);
    }
    return '—';
  }

  private ensureUsers(onReady: () => void): void {
    if (this.usersCache.length) {
      onReady();
      return;
    }
    this.users.list({ status: 'active', pageSize: 0 }).subscribe({
      next: (page) => {
        this.usersCache = page.items;
        onReady();
      },
      error: () => onReady(),
    });
  }
}
