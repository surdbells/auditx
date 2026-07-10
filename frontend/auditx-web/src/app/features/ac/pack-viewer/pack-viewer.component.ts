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

import { AcService } from '../../../core/services/ac.service';
import { UserLookupService } from '../../../core/services/user-lookup.service';
import { NotificationService } from '../../../core/services/notification.service';
import { AuthService } from '../../../core/services/auth.service';
import { Permissions } from '../../../core/permissions';
import {
  AcPack,
  AcPackAnalytics,
  AcPackDistribution,
  AcProducedArtefact,
} from '../../../core/models';
import { humanise, shortHash } from '../format';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';
import { downloadBlobResponse } from '../../reports/download';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { PaginatorComponent } from '../../../shared/components/paginator/paginator.component';
import { PageGuideComponent } from '../../../shared/components/page-guide/page-guide.component';
import { PageGuide } from '../../../core/models/page-guide.models';
import { AcAnalyticsSectionsComponent } from '../components/analytics-sections/analytics-sections.component';
import { AcCommentsComponent } from '../components/ac-comments/ac-comments.component';
import {
  CiaTextDialogComponent,
  CiaTextDialogData,
} from '../dialogs/cia-text-dialog.component';
import {
  ConfirmDialogComponent,
  ConfirmDialogData,
} from '../../../shared/components/confirm-dialog/confirm-dialog.component';

type ViewState = 'loading' | 'ready' | 'error';

const DEFAULT_PAGE_SIZE = 25;

/** Contextual page guide for the AC pack viewer (drives the walkthrough + the About panel). */
const PACK_VIEWER_GUIDE: PageGuide = {
  id: 'ac-pack-viewer',
  titleKey: 'ac.viewer.title',
  purposeKey: 'ac.packViewer.guide.purpose',
  descriptionKey: 'ac.packViewer.guide.description',
  actionKeys: [
    'ac.packViewer.guide.action.download',
    'ac.packViewer.guide.action.narrative',
    'ac.packViewer.guide.action.approve',
    'ac.packViewer.guide.action.distribute',
  ],
  sections: [
    { selector: '.viewer__actions', titleKey: 'ac.packViewer.guide.section.actions.title', bodyKey: 'ac.packViewer.guide.section.actions.body' },
    { selector: '.viewer__card', titleKey: 'ac.packViewer.guide.section.overview.title', bodyKey: 'ac.packViewer.guide.section.overview.body' },
    { selector: 'app-ac-analytics-sections', titleKey: 'ac.packViewer.guide.section.analytics.title', bodyKey: 'ac.packViewer.guide.section.analytics.body' },
    { selector: '.viewer__table-card', titleKey: 'ac.packViewer.guide.section.distribution.title', bodyKey: 'ac.packViewer.guide.section.distribution.body' },
  ],
  workflowKeys: [
    'ac.packViewer.guide.flow.generate',
    'ac.packViewer.guide.flow.review',
    'ac.packViewer.guide.flow.approve',
    'ac.packViewer.guide.flow.distribute',
    'ac.packViewer.guide.flow.comment',
  ],
  dependsOnKeys: [
    'ac.packViewer.guide.dep.exceptions',
    'ac.packViewer.guide.dep.sanctions',
    'ac.packViewer.guide.dep.analytics',
  ],
  usedByKeys: [
    'ac.packViewer.guide.use.committee',
    'ac.packViewer.guide.use.distribution',
    'ac.packViewer.guide.use.comments',
  ],
  businessRuleKeys: [
    'ac.packViewer.guide.rule.editState',
    'ac.packViewer.guide.rule.approveState',
    'ac.packViewer.guide.rule.distributeState',
    'ac.packViewer.guide.rule.hash',
  ],
  tipKeys: [
    'ac.packViewer.guide.tip.narrative',
    'ac.packViewer.guide.tip.hash',
    'ac.packViewer.guide.tip.log',
  ],
  permissionKeys: [
    'ac.packViewer.guide.perm.cia',
    'ac.packViewer.guide.perm.viewer',
  ],
  faq: [
    { questionKey: 'ac.packViewer.guide.faq.disabled.q', answerKey: 'ac.packViewer.guide.faq.disabled.a' },
    { questionKey: 'ac.packViewer.guide.faq.hash.q', answerKey: 'ac.packViewer.guide.faq.hash.a' },
  ],
};

@Component({
  selector: 'app-ac-pack-viewer',
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
    PaginatorComponent,
    PageGuideComponent,
    AcAnalyticsSectionsComponent,
    AcCommentsComponent,
    TranslatePipe,
  ],
  templateUrl: './pack-viewer.component.html',
  styleUrl: './pack-viewer.component.scss',
})
export class AcPackViewerComponent {
  /** Route param bound via withComponentInputBinding. */
  readonly id = input.required<string>();

  private readonly service = inject(AcService);
  /** Resolves distribution recipient user ids to display names. */
  readonly userLookup = inject(UserLookupService);
  private readonly notify = inject(NotificationService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly i18n = inject(TranslationService);

  readonly guide = PACK_VIEWER_GUIDE;

  readonly distributionColumns = ['recipient', 'version', 'dispatchedAt', 'outcome'];

  readonly state = signal<ViewState>('loading');
  readonly pack = signal<AcPack | null>(null);
  readonly analytics = signal<AcPackAnalytics | null>(null);

  readonly distributions = signal<AcPackDistribution[]>([]);
  readonly distributionsTotal = signal(0);
  readonly distributionsPage = signal(1);
  readonly distributionsPageSize = signal(DEFAULT_PAGE_SIZE);
  /** In-flight distribution-log fetch — disables that paginator without clearing the table. */
  readonly distributionsLoading = signal(false);

  readonly busy = signal(false);

  readonly humanise = humanise;
  readonly shortHash = shortHash;

  /** CIA-only review controls (edit text, approve, distribute). */
  readonly canCia = computed(() => this.auth.hasPermission(Permissions.CIA));

  readonly status = computed(() => this.pack()?.status ?? null);
  readonly canEditText = computed(
    () => this.canCia() && this.status() === 'pending_review',
  );
  readonly canApprove = computed(
    () => this.canCia() && this.status() === 'pending_review',
  );
  readonly canDistribute = computed(
    () => this.canCia() && this.status() === 'approved',
  );

  readonly artefacts = computed<AcProducedArtefact[]>(
    () => this.pack()?.producedArtefacts ?? [],
  );

  constructor() {
    queueMicrotask(() => this.fetch());
  }

  fetch(): void {
    this.state.set('loading');
    this.service.getPack(this.id()).subscribe({
      next: (pack) => {
        this.pack.set(pack);
        this.state.set('ready');
        this.loadAnalytics();
        this.fetchDistributionsPage(1);
      },
      error: () => this.state.set('error'),
    });
  }

  private reloadPack(): void {
    this.service.getPack(this.id()).subscribe({
      next: (pack) => this.pack.set(pack),
      error: () => {
        // Non-fatal: keep the existing view.
      },
    });
  }

  private loadAnalytics(): void {
    this.service.getPackAnalytics(this.id()).subscribe({
      next: (analytics) => this.analytics.set(analytics),
      error: () => {
        // Non-fatal: the snapshot may not be ready before completion.
      },
    });
  }

  /* ---- Download ---- */

  download(artefact: AcProducedArtefact): void {
    this.service.downloadPack(this.id(), artefact.format).subscribe({
      next: (res) => this.handleDownload(res, artefact.format),
      error: () =>
        this.notify.error(this.i18n.translate('ac.viewer.notify.downloadError')),
    });
  }

  private handleDownload(res: HttpResponse<Blob>, format: string): void {
    const version = this.pack()?.versionNumber ?? 0;
    const fallback = `ac-pack-v${version}.${format}`;
    if (!downloadBlobResponse(res, fallback)) {
      this.notify.error(this.i18n.translate('ac.viewer.notify.noContent'));
    }
  }

  /* ---- CIA: edit supplementary text ---- */

  editCiaText(): void {
    const data: CiaTextDialogData = {
      supplementaryText: this.pack()?.ciaSupplementaryText ?? null,
    };
    this.dialog
      .open(CiaTextDialogComponent, { data, width: '560px' })
      .afterClosed()
      .subscribe((text: string | undefined) => {
        if (text === undefined) {
          return;
        }
        this.service
          .updateCiaText(this.id(), { supplementaryText: text || null })
          .subscribe({
            next: (pack) => {
              this.pack.set(pack);
              this.notify.success(
                this.i18n.translate('ac.viewer.notify.narrativeSaved'),
              );
            },
            error: () =>
              this.notify.error(
                this.i18n.translate('ac.viewer.notify.narrativeSaveError'),
              ),
          });
      });
  }

  /* ---- CIA: approve ---- */

  approve(): void {
    if (this.busy()) {
      return;
    }
    const data: ConfirmDialogData = {
      title: 'Approve AC pack',
      message:
        'Approving seals this pack version for distribution to the committee. Continue?',
      confirmLabel: 'Approve',
    };
    this.dialog
      .open(ConfirmDialogComponent, { data })
      .afterClosed()
      .subscribe((ok) => {
        if (!ok) {
          return;
        }
        this.busy.set(true);
        this.service.approvePack(this.id()).subscribe({
          next: (pack) => {
            this.pack.set(pack);
            this.busy.set(false);
            this.notify.success('AC pack approved.');
          },
          error: () => {
            this.busy.set(false);
            this.notify.error('We could not approve the pack.');
          },
        });
      });
  }

  /* ---- CIA: distribute ---- */

  distribute(): void {
    if (this.busy()) {
      return;
    }
    const data: ConfirmDialogData = {
      title: 'Distribute AC pack',
      message: 'Distribute this approved pack to all audit-committee recipients?',
      confirmLabel: 'Distribute',
    };
    this.dialog
      .open(ConfirmDialogComponent, { data })
      .afterClosed()
      .subscribe((ok) => {
        if (!ok) {
          return;
        }
        this.busy.set(true);
        this.service.distributePack(this.id()).subscribe({
          next: (result) => {
            this.busy.set(false);
            this.notify.success(
              `AC pack distributed to ${result.recipientCount} recipient(s).`,
            );
            this.reloadPack();
            this.fetchDistributionsPage(1);
          },
          error: () => {
            this.busy.set(false);
            this.notify.error('We could not distribute the pack.');
          },
        });
      });
  }

  /* ---- Distribution log ---- */

  fetchDistributionsPage(page: number): void {
    this.distributionsLoading.set(true);
    this.service
      .packDistributions(this.id(), page, this.distributionsPageSize())
      .subscribe({
        next: (result) => {
          this.distributions.set(result.items);
          this.distributionsTotal.set(result.total);
          this.distributionsPage.set(result.page);
          this.distributionsLoading.set(false);
        },
        error: () => {
          // Non-fatal: leave the log empty.
          this.distributionsLoading.set(false);
        },
      });
  }

  onDistributionsPageChange(page: number): void {
    this.fetchDistributionsPage(page);
  }

  onDistributionsPageSizeChange(size: number): void {
    this.distributionsPageSize.set(size);
    this.fetchDistributionsPage(1);
  }
}