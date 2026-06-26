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
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';

type ViewState = 'loading' | 'ready' | 'error';

const PAGE_LIMIT = 100;

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

  readonly displayedColumns = [
    'eventType',
    'status',
    'attempts',
    'nextRetryAt',
    'lastError',
    'actions',
  ];

  readonly statuses: { value: WebhookDeliveryStatus | ''; label: string }[] = [
    { value: '', label: 'All statuses' },
    { value: 'Pending', label: 'Pending' },
    { value: 'Delivered', label: 'Delivered' },
    { value: 'Failed', label: 'Failed' },
    { value: 'DeadLetter', label: 'Dead-letter' },
  ];

  readonly statusFilter = new FormControl<WebhookDeliveryStatus | ''>('', {
    nonNullable: true,
  });

  readonly state = signal<ViewState>('loading');
  readonly deliveries = signal<WebhookDelivery[]>([]);

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
    this.webhooksService
      .listDeliveries(this.statusFilter.value, PAGE_LIMIT)
      .subscribe({
        next: (items) => {
          this.deliveries.set(items);
          this.state.set('ready');
        },
        error: () => this.state.set('error'),
      });
  }

  canRetryRow(delivery: WebhookDelivery): boolean {
    return (
      this.canRetry() &&
      (delivery.status === 'DeadLetter' || delivery.status === 'Failed')
    );
  }

  retry(delivery: WebhookDelivery): void {
    this.webhooksService.retryDelivery(delivery.id).subscribe({
      next: () => {
        this.notify.success('Delivery re-queued for retry.');
        this.fetch();
      },
    });
  }
}
