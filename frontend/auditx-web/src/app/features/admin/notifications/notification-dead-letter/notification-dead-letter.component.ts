import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';

import { NotificationAdminService } from '../../../../core/services/notifications-admin.service';
import { NotificationService } from '../../../../core/services/notification.service';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { AuthService } from '../../../../core/services/auth.service';
import { Permissions } from '../../../../core/permissions';
import { NotificationDispatch } from '../../../../core/models';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { PaginatorComponent } from '../../../../shared/components/paginator/paginator.component';

type ViewState = 'loading' | 'ready' | 'error';

const DEFAULT_PAGE_SIZE = 25;

@Component({
  selector: 'app-notification-dead-letter',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    TranslatePipe,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PaginatorComponent,
  ],
  templateUrl: './notification-dead-letter.component.html',
  styleUrl: './notification-dead-letter.component.scss',
})
export class NotificationDeadLetterComponent {
  private readonly notifications = inject(NotificationAdminService);
  private readonly notify = inject(NotificationService);
  private readonly i18n = inject(TranslationService);
  private readonly auth = inject(AuthService);

  readonly displayedColumns = [
    'eventType',
    'recipient',
    'channel',
    'attempts',
    'lastError',
    'actions',
  ];

  readonly state = signal<ViewState>('loading');
  readonly dispatches = signal<NotificationDispatch[]>([]);
  readonly total = signal(0);
  readonly page = signal(1);
  readonly pageSize = signal(DEFAULT_PAGE_SIZE);
  /** In-flight fetch (page navigation / refresh) — disables the paginator without clearing the table. */
  readonly loading = signal(false);

  readonly canRetry = computed(() =>
    this.auth.hasPermission(Permissions.AdminOps),
  );

  readonly isEmpty = computed(
    () => this.state() === 'ready' && this.dispatches().length === 0,
  );

  constructor() {
    this.fetchPage(1);
  }

  fetchPage(page: number): void {
    this.loading.set(true);
    this.notifications.listDeadLetter(page, this.pageSize()).subscribe({
      next: (result) => {
        this.dispatches.set(result.items);
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

  retry(dispatch: NotificationDispatch): void {
    this.notifications.retryDispatch(dispatch.id).subscribe({
      next: () => {
        this.notify.success(
          this.i18n.translate('notifications.deadLetter.toast.retried'),
        );
        this.fetchPage(this.page());
      },
    });
  }
}
