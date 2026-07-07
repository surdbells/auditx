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
import { downloadBlobResponse } from '../../reports/download';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
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

const DISTRIBUTIONS_PAGE_SIZE = 20;

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
    AcAnalyticsSectionsComponent,
    AcCommentsComponent,
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

  readonly distributionColumns = ['recipient', 'version', 'dispatchedAt', 'outcome'];

  readonly state = signal<ViewState>('loading');
  readonly pack = signal<AcPack | null>(null);
  readonly analytics = signal<AcPackAnalytics | null>(null);

  readonly distributions = signal<AcPackDistribution[]>([]);
  readonly nextCursor = signal<string | null>(null);
  readonly hasMore = signal(false);
  readonly loadingMore = signal(false);

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
        this.loadDistributions();
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
      error: () => this.notify.error('We could not download the pack.'),
    });
  }

  private handleDownload(res: HttpResponse<Blob>, format: string): void {
    const version = this.pack()?.versionNumber ?? 0;
    const fallback = `ac-pack-v${version}.${format}`;
    if (!downloadBlobResponse(res, fallback)) {
      this.notify.error('The download returned no content.');
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
              this.notify.success('Supplementary narrative saved.');
            },
            error: () =>
              this.notify.error('We could not save the supplementary text.'),
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
            this.reloadDistributions();
          },
          error: () => {
            this.busy.set(false);
            this.notify.error('We could not distribute the pack.');
          },
        });
      });
  }

  /* ---- Distribution log ---- */

  private reloadDistributions(): void {
    this.distributions.set([]);
    this.nextCursor.set(null);
    this.hasMore.set(false);
    this.loadDistributions();
  }

  private loadDistributions(): void {
    this.service
      .packDistributions(this.id(), null, DISTRIBUTIONS_PAGE_SIZE)
      .subscribe({
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
      .packDistributions(this.id(), this.nextCursor(), DISTRIBUTIONS_PAGE_SIZE)
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
}