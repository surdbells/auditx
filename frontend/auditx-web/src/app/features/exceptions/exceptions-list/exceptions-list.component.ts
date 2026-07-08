import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';
import { debounceTime } from 'rxjs';

import { ExceptionsService } from '../../../core/services/exceptions.service';
import { UsersService } from '../../../core/services/users.service';
import {
  ExceptionListItem,
  ExceptionSeverity,
  ExceptionStatus,
  UserDto,
} from '../../../core/models';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';

type ViewState = 'loading' | 'ready' | 'error';

const PAGE_SIZE = 20;

@Component({
  selector: 'app-exceptions-list',
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
    MatTooltipModule,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PageHeaderComponent,
    TranslatePipe,
  ],
  templateUrl: './exceptions-list.component.html',
  styleUrl: './exceptions-list.component.scss',
})
export class ExceptionsListComponent {
  private readonly service = inject(ExceptionsService);
  private readonly users = inject(UsersService);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);
  private readonly i18n = inject(TranslationService);

  readonly displayedColumns = [
    'title',
    'severity',
    'status',
    'owner',
    'targetDate',
    'flags',
  ];

  readonly statuses: { value: ExceptionStatus | 'all'; label: string }[] = [
    { value: 'all', label: this.i18n.translate('exceptions.filter.allStatuses') },
    { value: 'open', label: this.i18n.translate('exceptions.status.open') },
    { value: 'map_submitted', label: this.i18n.translate('exceptions.status.mapSubmitted') },
    { value: 'map_approved', label: this.i18n.translate('exceptions.status.mapApproved') },
    { value: 'map_rejected', label: this.i18n.translate('exceptions.status.mapRejected') },
    { value: 'pending_closure', label: this.i18n.translate('exceptions.status.pendingClosure') },
    { value: 'closed', label: this.i18n.translate('exceptions.status.closed') },
    { value: 'cancelled', label: this.i18n.translate('exceptions.status.cancelled') },
  ];

  readonly severities: { value: ExceptionSeverity | 'all'; label: string }[] = [
    { value: 'all', label: this.i18n.translate('exceptions.filter.allSeverities') },
    { value: 'low', label: this.i18n.translate('exceptions.severity.low') },
    { value: 'medium', label: this.i18n.translate('exceptions.severity.medium') },
    { value: 'high', label: this.i18n.translate('exceptions.severity.high') },
    { value: 'critical', label: this.i18n.translate('exceptions.severity.critical') },
  ];

  readonly boolFilters: { value: 'all' | 'yes' | 'no'; label: string }[] = [
    { value: 'all', label: this.i18n.translate('exceptions.filter.all') },
    { value: 'yes', label: this.i18n.translate('exceptions.filter.yes') },
    { value: 'no', label: this.i18n.translate('exceptions.filter.no') },
  ];

  readonly filters = this.fb.nonNullable.group({
    status: 'all' as ExceptionStatus | 'all',
    severity: 'all' as ExceptionSeverity | 'all',
    overdue: 'all' as 'all' | 'yes' | 'no',
    recurrence: 'all' as 'all' | 'yes' | 'no',
  });

  readonly state = signal<ViewState>('loading');
  readonly exceptions = signal<ExceptionListItem[]>([]);
  readonly nextCursor = signal<string | null>(null);
  readonly hasMore = signal(false);
  readonly loadingMore = signal(false);
  readonly userNames = signal<Record<string, string>>({});

  readonly isEmpty = computed(
    () => this.state() === 'ready' && this.exceptions().length === 0,
  );

  private usersCache: UserDto[] = [];

  constructor() {
    this.ensureUsers();
    this.fetchFirstPage();
    this.filters.valueChanges
      .pipe(debounceTime(300), takeUntilDestroyed())
      .subscribe(() => this.fetchFirstPage());
  }

  private ensureUsers(): void {
    this.users.list({ status: 'active', limit: 200 }).subscribe({
      next: (page) => {
        this.usersCache = page.items;
        const map: Record<string, string> = {};
        for (const u of page.items) {
          map[u.id] = u.displayName;
        }
        this.userNames.set(map);
      },
      error: () => {
        // Non-fatal: ids display verbatim.
      },
    });
  }

  nameOf(userId: string | null | undefined): string {
    if (!userId) {
      return '—';
    }
    return this.userNames()[userId] ?? userId;
  }

  fetchFirstPage(): void {
    this.state.set('loading');
    this.exceptions.set([]);
    this.nextCursor.set(null);
    this.query(null, (items, cursor, more) => {
      this.exceptions.set(items);
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
      this.exceptions.update((current) => [...current, ...items]);
      this.nextCursor.set(cursor);
      this.hasMore.set(more);
      this.loadingMore.set(false);
    });
  }

  private query(
    cursor: string | null,
    onSuccess: (
      items: ExceptionListItem[],
      cursor: string | null,
      more: boolean,
    ) => void,
  ): void {
    const { status, severity, overdue, recurrence } = this.filters.getRawValue();
    this.service
      .list({
        status: status === 'all' ? '' : status,
        severity: severity === 'all' ? '' : severity,
        overdue: overdue === 'all' ? undefined : overdue === 'yes',
        recurrence: recurrence === 'all' ? undefined : recurrence === 'yes',
        cursor,
        limit: PAGE_SIZE,
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

  open(row: ExceptionListItem): void {
    void this.router.navigate(['/exceptions', row.id]);
  }
}
