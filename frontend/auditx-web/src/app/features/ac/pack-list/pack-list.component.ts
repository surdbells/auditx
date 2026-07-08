import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import { Subscription, timer } from 'rxjs';

import { AcService } from '../../../core/services/ac.service';
import { NotificationService } from '../../../core/services/notification.service';
import { AuthService } from '../../../core/services/auth.service';
import { Permissions } from '../../../core/permissions';
import { AcPack, AcPackListItem } from '../../../core/models';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';
import { humanise, shortHash } from '../format';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { GeneratePackDialogComponent } from '../dialogs/generate-pack-dialog.component';

type ViewState = 'loading' | 'ready' | 'error';

/** Poll interval (ms) while a generation is pending/generating. */
const POLL_INTERVAL = 2000;
const PAGE_SIZE = 20;

/** Pack statuses the filter offers. */
const STATUS_OPTIONS = [
  '',
  'pending',
  'generating',
  'pending_review',
  'approved',
  'distributed',
  'failed',
];

@Component({
  selector: 'app-ac-pack-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    RouterLink,
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatSelectModule,
    MatTooltipModule,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PageHeaderComponent,
    TranslatePipe,
  ],
  templateUrl: './pack-list.component.html',
  styleUrl: './pack-list.component.scss',
})
export class AcPackListComponent {
  private readonly service = inject(AcService);
  private readonly notify = inject(NotificationService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly destroyRef = inject(DestroyRef);
  private readonly i18n = inject(TranslationService);

  readonly statusOptions = STATUS_OPTIONS;
  readonly displayedColumns = [
    'version',
    'period',
    'meeting',
    'status',
    'hash',
    'requestedAt',
  ];

  readonly state = signal<ViewState>('loading');
  readonly packs = signal<AcPackListItem[]>([]);
  readonly statusFilter = signal('');
  readonly nextCursor = signal<string | null>(null);
  readonly hasMore = signal(false);
  readonly loadingMore = signal(false);
  readonly generating = signal(false);

  private pollSub: Subscription | null = null;

  readonly humanise = humanise;
  readonly shortHash = shortHash;

  readonly canGenerate = computed(() =>
    this.auth.hasPermission(Permissions.GenerateACPack),
  );

  readonly isEmpty = computed(
    () => this.state() === 'ready' && this.packs().length === 0,
  );

  constructor() {
    queueMicrotask(() => this.load());
    this.destroyRef.onDestroy(() => this.pollSub?.unsubscribe());
  }

  load(): void {
    this.state.set('loading');
    this.service
      .listPacks({ status: this.statusFilter() || null, limit: PAGE_SIZE })
      .subscribe({
        next: (page) => {
          this.packs.set(page.items);
          this.nextCursor.set(page.nextCursor);
          this.hasMore.set(page.hasMore);
          this.state.set('ready');
        },
        error: () => this.state.set('error'),
      });
  }

  onStatusChange(status: string): void {
    this.statusFilter.set(status);
    this.load();
  }

  loadMore(): void {
    if (!this.hasMore() || this.loadingMore()) {
      return;
    }
    this.loadingMore.set(true);
    this.service
      .listPacks({
        status: this.statusFilter() || null,
        cursor: this.nextCursor(),
        limit: PAGE_SIZE,
      })
      .subscribe({
        next: (page) => {
          this.packs.update((current) => [...current, ...page.items]);
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
    this.dialog
      .open(GeneratePackDialogComponent, { width: '480px' })
      .afterClosed()
      .subscribe((request) => {
        if (!request) {
          return;
        }
        this.generating.set(true);
        this.service.generatePack(request).subscribe({
          next: (result) => {
            this.notify.info(this.i18n.translate('ac.packs.notify.started'));
            this.pollUntilSettled(result.acPackId);
          },
          error: () => {
            this.generating.set(false);
            this.notify.error(this.i18n.translate('ac.packs.notify.startError'));
          },
        });
      });
  }

  /** Polls GET /ac-packs/{id} every ~2s until it leaves pending/generating. */
  private pollUntilSettled(packId: string): void {
    this.pollSub?.unsubscribe();
    this.pollSub = timer(0, POLL_INTERVAL).subscribe(() => {
      this.service.getPack(packId).subscribe({
        next: (pack) => this.onPoll(pack),
        error: () => {
          this.pollSub?.unsubscribe();
          this.generating.set(false);
          this.notify.error(this.i18n.translate('ac.packs.notify.lostTrack'));
          this.load();
        },
      });
    });
  }

  private onPoll(pack: AcPack): void {
    if (pack.status === 'failed') {
      this.pollSub?.unsubscribe();
      this.generating.set(false);
      this.notify.error(
        pack.failureReason || this.i18n.translate('ac.packs.notify.failed'),
      );
      this.load();
    } else if (pack.status !== 'pending' && pack.status !== 'generating') {
      // pending_review (or beyond): ready for CIA review.
      this.pollSub?.unsubscribe();
      this.generating.set(false);
      this.notify.success(
        this.i18n.translate('ac.packs.notify.ready', {
          version: pack.versionNumber,
        }),
      );
      this.load();
    }
    // pending / generating: keep polling.
  }
}