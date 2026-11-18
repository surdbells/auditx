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
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
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
  ReportComment,
  ReportDistribution,
  ReportHashVerification,
  UserDirectoryEntry,
} from '../../../core/models';
import { humanise, shortHash } from '../humanise';
import { downloadBlobResponse } from '../download';
import { printReportHtml } from '../print';
import {
  DistributeReportDialogComponent,
  DistributeReportDialogData,
} from '../dialogs/distribute-report-dialog.component';
import { ReportShareDialogComponent } from '../dialogs/report-share-dialog.component';
import {
  ReportSignOffDialogComponent,
  ReportSignOffDialogData,
} from '../dialogs/report-sign-off-dialog.component';
import { reviewBadge } from '../review-badge';
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
    ReactiveFormsModule,
    MatFormFieldModule,
    MatInputModule,
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

  private usersCache: UserDirectoryEntry[] = [];

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

  /* ---- Draft-to-final review ---- */

  private readonly myUserId = computed(() => this.auth.session()?.userId ?? null);
  /** Engagement reports go through draft and final; standalone reports do not. */
  readonly isEngagement = computed(() => !!this.report()?.auditId);
  readonly isDraft = computed(() => this.report()?.stage === 'draft');
  readonly badge = computed(() => reviewBadge(this.report() ?? {}));
  /** The signed-in user generated this report, so someone else must sign it off. */
  readonly preparedByMe = computed(() => !!this.report() && this.report()!.generatedBy === this.myUserId());
  /** The Chief Internal Auditor decides a final report awaiting sign-off — unless they prepared it. */
  readonly canDecide = computed(
    () =>
      this.auth.hasPermission(Permissions.CIA) &&
      this.isEngagement() &&
      this.report()?.signOffStatus === 'awaiting' &&
      !this.preparedByMe(),
  );
  readonly deciding = signal(false);
  /** Distribution is offered for a completed draft, or a final once it has been signed off. */
  readonly canDistributeNow = computed(
    () => this.canDistribute() && this.isCompleted() && this.report()?.isDistributable === true,
  );

  readonly comments = signal<ReportComment[]>([]);
  readonly posting = signal(false);
  readonly commentText = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.maxLength(4000)],
  });
  /** Drafts are discussed: comments are shown and taken once the draft is ready. */
  readonly showComments = computed(() => this.isEngagement() && this.isDraft() && this.isCompleted());

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
        this.fetchDistributions(1);
        this.loadComments();
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

  /* ---- Review: sign-off and draft comments ---- */

  /** Sign the final off, or return it for changes, after confirming in a dialog. */
  decide(mode: 'sign_off' | 'return'): void {
    const r = this.report();
    if (!r || this.deciding()) {
      return;
    }
    const data: ReportSignOffDialogData = { mode, versionNumber: r.versionNumber };
    this.dialog
      .open(ReportSignOffDialogComponent, { data, width: '480px', maxWidth: '95vw' })
      .afterClosed()
      .subscribe((text?: string | null) => {
        if (text === undefined) {
          return; // cancelled
        }
        this.deciding.set(true);
        const call =
          mode === 'sign_off' ? this.service.signOff(r.id, text) : this.service.returnForChanges(r.id, text ?? '');
        call.subscribe({
          next: (updated) => {
            this.report.set(updated);
            this.deciding.set(false);
            this.notify.success(
              this.i18n.translate(mode === 'sign_off' ? 'reports.review.notify.signedOff' : 'reports.review.notify.returned'),
            );
          },
          // The error interceptor shows the server's reason (e.g. "you generated this report").
          error: () => this.deciding.set(false),
        });
      });
  }

  private loadComments(): void {
    if (!this.showComments()) {
      this.comments.set([]);
      return;
    }
    this.service.comments(this.id()).subscribe({
      next: (comments) => this.comments.set(comments),
      error: () => {
        // Non-fatal: the thread just stays empty.
      },
    });
  }

  postComment(): void {
    if (this.commentText.invalid || this.posting()) {
      this.commentText.markAsTouched();
      return;
    }
    const body = this.commentText.value.trim();
    if (!body) {
      return;
    }
    this.posting.set(true);
    this.service.addComment(this.id(), body).subscribe({
      next: (comment) => {
        this.comments.update((list) => [...list, comment]);
        this.commentText.reset('');
        this.posting.set(false);
      },
      error: () => this.posting.set(false),
    });
  }

  nameOf(userId: string | null | undefined): string {
    return this.userLookup.displayName(userId);
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
    // Active people, names only — readable by anyone signed in (the full user list is an administrator's).
    this.users.activeDirectory().subscribe({
      next: (people) => {
        this.usersCache = people;
        onReady();
      },
      error: () => onReady(),
    });
  }
}
