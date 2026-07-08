import {
  ChangeDetectionStrategy,
  Component,
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

import { AcService } from '../../../core/services/ac.service';
import { UsersService } from '../../../core/services/users.service';
import { NotificationService } from '../../../core/services/notification.service';
import { AuthService } from '../../../core/services/auth.service';
import { Permissions } from '../../../core/permissions';
import { AcActionItem, UserDto } from '../../../core/models';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';
import { humanise } from '../format';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import {
  CreateActionItemDialogComponent,
  CreateActionItemDialogData,
} from '../dialogs/create-action-item-dialog.component';
import { CloseActionItemDialogComponent } from '../dialogs/close-action-item-dialog.component';

type ViewState = 'loading' | 'ready' | 'error';

const PAGE_SIZE = 7;
const STATUS_OPTIONS = ['', 'open', 'in_progress', 'closed', 'acknowledged'];

@Component({
  selector: 'app-ac-action-items',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatSelectModule,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PageHeaderComponent,
    TranslatePipe,
  ],
  templateUrl: './action-items.component.html',
  styleUrl: './action-items.component.scss',
})
export class AcActionItemsComponent {
  private readonly service = inject(AcService);
  private readonly users = inject(UsersService);
  private readonly notify = inject(NotificationService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly i18n = inject(TranslationService);

  readonly statusOptions = STATUS_OPTIONS;
  readonly displayedColumns = [
    'title',
    'status',
    'assignee',
    'dueDate',
    'actions',
  ];

  readonly state = signal<ViewState>('loading');
  readonly items = signal<AcActionItem[]>([]);
  readonly statusFilter = signal('');
  readonly nextCursor = signal<string | null>(null);
  readonly hasMore = signal(false);
  readonly loadingMore = signal(false);

  private usersCache: UserDto[] = [];

  readonly humanise = humanise;

  /** ACMember: create items. CIA: progress/close. ACChair: acknowledge. */
  readonly canCreate = computed(() =>
    this.auth.hasPermission(Permissions.ACMember),
  );
  readonly canManage = computed(() => this.auth.hasPermission(Permissions.CIA));
  readonly canAcknowledge = computed(() =>
    this.auth.hasPermission(Permissions.ACChair),
  );

  readonly isEmpty = computed(
    () => this.state() === 'ready' && this.items().length === 0,
  );

  constructor() {
    queueMicrotask(() => this.load());
  }

  load(): void {
    this.state.set('loading');
    this.service
      .listActionItems(this.statusFilter() || null, null, PAGE_SIZE)
      .subscribe({
        next: (page) => {
          this.items.set(page.items);
          this.nextCursor.set(page.nextCursor);
          this.hasMore.set(page.hasMore);
          this.state.set('ready');
          this.ensureUsers();
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
      .listActionItems(this.statusFilter() || null, this.nextCursor(), PAGE_SIZE)
      .subscribe({
        next: (page) => {
          this.items.update((current) => [...current, ...page.items]);
          this.nextCursor.set(page.nextCursor);
          this.hasMore.set(page.hasMore);
          this.loadingMore.set(false);
        },
        error: () => this.loadingMore.set(false),
      });
  }

  /* ---- Create (ACMember) ---- */

  create(): void {
    this.ensureUsers(() => {
      const data: CreateActionItemDialogData = { users: this.usersCache };
      this.dialog
        .open(CreateActionItemDialogComponent, { data, width: '520px' })
        .afterClosed()
        .subscribe((request) => {
          if (!request) {
            return;
          }
          this.service.createActionItem(request).subscribe({
            next: () => {
              this.notify.success(this.i18n.translate('ac.items.notify.created'));
              this.load();
            },
            error: () =>
              this.notify.error(
                this.i18n.translate('ac.items.notify.createError'),
              ),
          });
        });
    });
  }

  /* ---- Progress / close (CIA) ---- */

  markInProgress(item: AcActionItem): void {
    this.service
      .updateActionItem(item.id, { markInProgress: true })
      .subscribe({
        next: (updated) => {
          this.replace(updated);
          this.notify.success(this.i18n.translate('ac.items.notify.inProgress'));
        },
        error: () =>
          this.notify.error(this.i18n.translate('ac.items.notify.updateError')),
      });
  }

  close(item: AcActionItem): void {
    this.dialog
      .open(CloseActionItemDialogComponent, { width: '520px' })
      .afterClosed()
      .subscribe((response: string | undefined) => {
        if (!response) {
          return;
        }
        this.service
          .updateActionItem(item.id, { closureResponse: response })
          .subscribe({
            next: (updated) => {
              this.replace(updated);
              this.notify.success(this.i18n.translate('ac.items.notify.closed'));
            },
            error: () =>
              this.notify.error(
                this.i18n.translate('ac.items.notify.closeError'),
              ),
          });
      });
  }

  /* ---- Acknowledge (ACChair) ---- */

  acknowledge(item: AcActionItem): void {
    this.service.acknowledgeActionItemClosure(item.id).subscribe({
      next: (updated) => {
        this.replace(updated);
        this.notify.success(this.i18n.translate('ac.items.notify.acknowledged'));
      },
      error: () =>
        this.notify.error(
          this.i18n.translate('ac.items.notify.acknowledgeError'),
        ),
    });
  }

  /* ---- Helpers ---- */

  assigneeLabel(item: AcActionItem): string {
    if (!item.assignedToUserId) {
      return '—';
    }
    return (
      this.usersCache.find((u) => u.id === item.assignedToUserId)?.displayName ??
      item.assignedToUserId
    );
  }

  private replace(updated: AcActionItem): void {
    this.items.update((list) =>
      list.map((i) => (i.id === updated.id ? updated : i)),
    );
  }

  private ensureUsers(onReady?: () => void): void {
    if (this.usersCache.length) {
      onReady?.();
      return;
    }
    this.users.list({ status: 'active', limit: 200 }).subscribe({
      next: (page) => {
        this.usersCache = page.items;
        onReady?.();
      },
      error: () => onReady?.(),
    });
  }
}