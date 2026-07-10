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
import { PaginatorComponent } from '../../../shared/components/paginator/paginator.component';
import { PageGuideComponent } from '../../../shared/components/page-guide/page-guide.component';
import { PageGuide } from '../../../core/models/page-guide.models';
import {
  CreateActionItemDialogComponent,
  CreateActionItemDialogData,
} from '../dialogs/create-action-item-dialog.component';
import { CloseActionItemDialogComponent } from '../dialogs/close-action-item-dialog.component';

type ViewState = 'loading' | 'ready' | 'error';

const DEFAULT_PAGE_SIZE = 25;
const STATUS_OPTIONS = ['', 'open', 'in_progress', 'closed', 'acknowledged'];

/** Contextual page guide for the AC action-items workspace (walkthrough + About panel). */
const AC_ACTION_ITEMS_GUIDE: PageGuide = {
  id: 'ac-action-items',
  titleKey: 'ac.items.title',
  purposeKey: 'ac.actionItems.guide.purpose',
  descriptionKey: 'ac.actionItems.guide.description',
  actionKeys: [
    'ac.actionItems.guide.action.create',
    'ac.actionItems.guide.action.filter',
    'ac.actionItems.guide.action.progress',
    'ac.actionItems.guide.action.acknowledge',
  ],
  sections: [
    { selector: '[data-guide="create"]', titleKey: 'ac.actionItems.guide.section.create.title', bodyKey: 'ac.actionItems.guide.section.create.body' },
    { selector: '.items__filter', titleKey: 'ac.actionItems.guide.section.filter.title', bodyKey: 'ac.actionItems.guide.section.filter.body' },
    { selector: '.items__table', titleKey: 'ac.actionItems.guide.section.table.title', bodyKey: 'ac.actionItems.guide.section.table.body' },
  ],
  workflowKeys: [
    'ac.actionItems.guide.flow.meeting',
    'ac.actionItems.guide.flow.raise',
    'ac.actionItems.guide.flow.progress',
    'ac.actionItems.guide.flow.close',
    'ac.actionItems.guide.flow.acknowledge',
  ],
  dependsOnKeys: [
    'ac.actionItems.guide.dep.committee',
    'ac.actionItems.guide.dep.users',
    'ac.actionItems.guide.dep.audits',
  ],
  usedByKeys: [
    'ac.actionItems.guide.use.minutes',
    'ac.actionItems.guide.use.reports',
    'ac.actionItems.guide.use.analytics',
  ],
  businessRuleKeys: [
    'ac.actionItems.guide.rule.lifecycle',
    'ac.actionItems.guide.rule.closure',
    'ac.actionItems.guide.rule.acknowledge',
    'ac.actionItems.guide.rule.roles',
  ],
  tipKeys: [
    'ac.actionItems.guide.tip.due',
    'ac.actionItems.guide.tip.filter',
  ],
  permissionKeys: [
    'ac.actionItems.guide.perm.member',
    'ac.actionItems.guide.perm.cia',
    'ac.actionItems.guide.perm.chair',
  ],
  faq: [
    { questionKey: 'ac.actionItems.guide.faq.actions.q', answerKey: 'ac.actionItems.guide.faq.actions.a' },
    { questionKey: 'ac.actionItems.guide.faq.acknowledge.q', answerKey: 'ac.actionItems.guide.faq.acknowledge.a' },
  ],
};

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
    PaginatorComponent,
    PageGuideComponent,
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
  readonly total = signal(0);
  readonly page = signal(1);
  readonly pageSize = signal(DEFAULT_PAGE_SIZE);
  /** In-flight fetch (page navigation / filter change) — disables the paginator without clearing the table. */
  readonly loading = signal(false);

  private usersCache: UserDto[] = [];

  readonly humanise = humanise;

  readonly guide = AC_ACTION_ITEMS_GUIDE;

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
    queueMicrotask(() => this.fetchPage(1));
  }

  fetchPage(page: number): void {
    this.loading.set(true);
    this.service
      .listActionItems(this.statusFilter() || null, page, this.pageSize())
      .subscribe({
        next: (result) => {
          this.items.set(result.items);
          this.total.set(result.total);
          this.page.set(result.page);
          this.state.set('ready');
          this.loading.set(false);
          this.ensureUsers();
        },
        error: () => {
          if (this.state() === 'loading') {
            this.state.set('error');
          }
          this.loading.set(false);
        },
      });
  }

  onStatusChange(status: string): void {
    this.statusFilter.set(status);
    this.fetchPage(1);
  }

  onPageChange(page: number): void {
    this.fetchPage(page);
  }

  onPageSizeChange(size: number): void {
    this.pageSize.set(size);
    this.fetchPage(1);
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
              this.fetchPage(1);
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
    this.users.list({ status: 'active', pageSize: 0 }).subscribe({
      next: (page) => {
        this.usersCache = page.items;
        onReady?.();
      },
      error: () => onReady?.(),
    });
  }
}