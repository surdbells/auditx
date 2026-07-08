import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';

import { WebhooksService } from '../../../../core/services/webhooks.service';
import { NotificationService } from '../../../../core/services/notification.service';
import { AuthService } from '../../../../core/services/auth.service';
import { Permissions } from '../../../../core/permissions';
import {
  WebhookDelivery,
  WebhookDeliveryStatus,
} from '../../../../core/models';
import { humaniseStatus } from '../../notifications/humanise-status';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';

type ViewState = 'loading' | 'ready' | 'error';

const PAGE_LIMIT = 7;

@Component({
  selector: 'app-webhook-deliveries',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    ReactiveFormsModule,
    MatCardModule,
    MatTableModule,
    MatFormFieldModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    TranslatePipe,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
  ],
  templateUrl: './webhook-deliveries.component.html',
  styleUrl: './webhook-deliveries.component.scss',
})
export class WebhookDeliveriesComponent {
  private readonly webhooksService = inject(WebhooksService);
  private readonly notify = inject(NotificationService);
  private readonly auth = inject(AuthService);
  private readonly i18n = inject(TranslationService);

  readonly displayedColumns = [
    'eventType',
    'status',
    'attempts',
    'nextRetryAt',
    'lastError',
    'actions',
  ];

  readonly statuses: { value: WebhookDeliveryStatus | ''; label: string }[] = [
    { value: '', label: this.i18n.translate('integrations.deliveries.status.all') },
    { value: 'pending', label: this.i18n.translate('integrations.deliveries.status.pending') },
    { value: 'delivered', label: this.i18n.translate('integrations.deliveries.status.delivered') },
    { value: 'failed', label: this.i18n.translate('integrations.deliveries.status.failed') },
    { value: 'dead_letter', label: this.i18n.translate('integrations.deliveries.status.deadLetter') },
  ];

  readonly statusFilter = new FormControl<WebhookDeliveryStatus | ''>('', {
    nonNullable: true,
  });

  readonly state = signal<ViewState>('loading');
  readonly deliveries = signal<WebhookDelivery[]>([]);
  readonly nextCursor = signal<string | null>(null);
  readonly hasMore = signal(false);
  readonly loadingMore = signal(false);

  readonly humanise = humaniseStatus;

  readonly canRetry = computed(() =>
    this.auth.hasPermission(Permissions.AdminOps),
  );

  readonly isEmpty = computed(
    () => this.state() === 'ready' && this.deliveries().length === 0,
  );

  constructor() {
    this.fetch();
    this.statusFilter.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe(() => this.fetch());
  }

  fetch(): void {
    this.state.set('loading');
    this.deliveries.set([]);
    this.nextCursor.set(null);
    this.query(null, (items, cursor, more) => {
      this.deliveries.set(items);
      this.nextCursor.set(cursor);
      this.hasMore.set(more);
      this.state.set('ready');
    });
  }

  loadMore(): void {
    if (!this.hasMore() || this.loadingMore()) {
      return;
    }
    this.loadingMore.set(true);
    this.query(this.nextCursor(), (items, cursor, more) => {
      this.deliveries.update((current) => [...current, ...items]);
      this.nextCursor.set(cursor);
      this.hasMore.set(more);
      this.loadingMore.set(false);
    });
  }

  private query(
    cursor: string | null,
    onSuccess: (
      items: WebhookDelivery[],
      cursor: string | null,
      more: boolean,
    ) => void,
  ): void {
    this.webhooksService
      .listDeliveries({
        status: this.statusFilter.value,
        cursor: cursor ?? undefined,
        limit: PAGE_LIMIT,
      })
      .subscribe({
        next: (page) => onSuccess(page.items, page.nextCursor, page.hasMore),
        error: () => {
          if (cursor === null) {
            this.state.set('error');
          } else {
            this.loadingMore.set(false);
          }
        },
      });
  }

  canRetryRow(delivery: WebhookDelivery): boolean {
    return (
      this.canRetry() &&
      (delivery.status === 'dead_letter' || delivery.status === 'failed')
    );
  }

  retry(delivery: WebhookDelivery): void {
    this.webhooksService.retryDelivery(delivery.id).subscribe({
      next: () => {
        this.notify.success(this.i18n.translate('integrations.deliveries.retry.success'));
        this.fetch();
      },
    });
  }
}
