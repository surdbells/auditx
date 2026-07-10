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
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';

import { NotificationAdminService } from '../../../../core/services/notifications-admin.service';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { DispatchStatus, NotificationDispatch } from '../../../../core/models';
import { humaniseStatus } from '../humanise-status';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { PaginatorComponent } from '../../../../shared/components/paginator/paginator.component';

type ViewState = 'loading' | 'ready' | 'error';

const DEFAULT_PAGE_SIZE = 25;

@Component({
  selector: 'app-notification-dispatches',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    ReactiveFormsModule,
    MatCardModule,
    MatTableModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    TranslatePipe,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PaginatorComponent,
  ],
  templateUrl: './notification-dispatches.component.html',
  styleUrl: './notification-dispatches.component.scss',
})
export class NotificationDispatchesComponent {
  private readonly notifications = inject(NotificationAdminService);
  private readonly i18n = inject(TranslationService);

  readonly displayedColumns = [
    'eventType',
    'recipient',
    'channel',
    'status',
    'attempts',
    'nextRetryAt',
    'deliveredAt',
    'lastError',
  ];

  readonly statuses: { value: DispatchStatus | ''; label: string }[] = [
    {
      value: '',
      label: this.i18n.translate('notifications.dispatches.status.all'),
    },
    {
      value: 'pending',
      label: this.i18n.translate('notifications.status.pending'),
    },
    {
      value: 'dispatched',
      label: this.i18n.translate('notifications.status.dispatched'),
    },
    {
      value: 'delivered',
      label: this.i18n.translate('notifications.status.delivered'),
    },
    {
      value: 'bounced',
      label: this.i18n.translate('notifications.status.bounced'),
    },
    {
      value: 'failed',
      label: this.i18n.translate('notifications.status.failed'),
    },
    {
      value: 'dead_letter',
      label: this.i18n.translate('notifications.status.deadLetter'),
    },
  ];

  readonly statusFilter = new FormControl<DispatchStatus | ''>('', {
    nonNullable: true,
  });

  readonly eventTypeFilter = new FormControl<string>('', {
    nonNullable: true,
  });

  readonly state = signal<ViewState>('loading');
  readonly dispatches = signal<NotificationDispatch[]>([]);
  readonly total = signal(0);
  readonly page = signal(1);
  readonly pageSize = signal(DEFAULT_PAGE_SIZE);
  /** In-flight fetch (page navigation / filter change) — disables the paginator without clearing the table. */
  readonly loading = signal(false);

  readonly humanise = humaniseStatus;

  readonly isEmpty = computed(
    () => this.state() === 'ready' && this.dispatches().length === 0,
  );

  constructor() {
    this.fetchPage(1);
    this.statusFilter.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe(() => this.fetchPage(1));
  }

  fetchPage(page: number): void {
    this.loading.set(true);
    this.notifications
      .listDispatches({
        status: this.statusFilter.value,
        eventType: this.eventTypeFilter.value.trim(),
        page,
        pageSize: this.pageSize(),
      })
      .subscribe({
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
}
