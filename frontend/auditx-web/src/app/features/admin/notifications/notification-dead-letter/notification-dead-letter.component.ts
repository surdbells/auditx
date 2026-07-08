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

type ViewState = 'loading' | 'ready' | 'error';

const PAGE_LIMIT = 100;

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

  readonly canRetry = computed(() =>
    this.auth.hasPermission(Permissions.AdminOps),
  );

  readonly isEmpty = computed(
    () => this.state() === 'ready' && this.dispatches().length === 0,
  );

  constructor() {
    this.fetch();
  }

  fetch(): void {
    this.state.set('loading');
    this.notifications.listDeadLetter(undefined, PAGE_LIMIT).subscribe({
      next: (page) => {
        this.dispatches.set(page.items);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  retry(dispatch: NotificationDispatch): void {
    this.notifications.retryDispatch(dispatch.id).subscribe({
      next: () => {
        this.notify.success(
          this.i18n.translate('notifications.deadLetter.toast.retried'),
        );
        this.fetch();
      },
    });
  }
}
