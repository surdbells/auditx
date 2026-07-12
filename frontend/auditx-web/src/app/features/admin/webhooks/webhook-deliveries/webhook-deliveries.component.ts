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
import { IconComponent } from '../../../../core/icons/icon.component';
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
import { PaginatorComponent } from '../../../../shared/components/paginator/paginator.component';

type ViewState = 'loading' | 'ready' | 'error';

const DEFAULT_PAGE_SIZE = 25;

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
    IconComponent,
    TranslatePipe,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PaginatorComponent,
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
  readonly total = signal(0);
  readonly page = signal(1);
  readonly pageSize = signal(DEFAULT_PAGE_SIZE);
  /** In-flight fetch (page navigation / filter change) — disables the paginator without clearing the table. */
  readonly loading = signal(false);

  readonly humanise = humaniseStatus;

  readonly canRetry = computed(() =>
    this.auth.hasPermission(Permissions.AdminOps),
  );

  readonly isEmpty = computed(
    () => this.state() === 'ready' && this.deliveries().length === 0,
  );

  constructor() {
    this.fetchPage(1);
    this.statusFilter.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe(() => this.fetchPage(1));
  }

  fetchPage(page: number): void {
    this.loading.set(true);
    this.webhooksService
      .listDeliveries({
        status: this.statusFilter.value,
        page,
        pageSize: this.pageSize(),
      })
      .subscribe({
        next: (result) => {
          this.deliveries.set(result.items);
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
        this.fetchPage(this.page());
      },
    });
  }
}
