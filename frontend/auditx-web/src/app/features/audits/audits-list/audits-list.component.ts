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
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { debounceTime } from 'rxjs';

import { AuditsService } from '../../../core/services/audits.service';
import { UsersService } from '../../../core/services/users.service';
import { NotificationService } from '../../../core/services/notification.service';
import { AuthService } from '../../../core/services/auth.service';
import { ReferenceDataLookupService } from '../../../core/services/reference-data-lookup.service';
import { Permissions } from '../../../core/permissions';
import {
  AuditListItem,
  AuditStatus,
  CreateAuditRequest,
  UserDto,
} from '../../../core/models';
import {
  CreateAuditDialogComponent,
  CreateAuditDialogData,
} from '../dialogs/create-audit-dialog.component';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';

type ViewState = 'loading' | 'ready' | 'error';

const PAGE_SIZE = 20;

@Component({
  selector: 'app-audits-list',
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
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PageHeaderComponent,
  ],
  templateUrl: './audits-list.component.html',
  styleUrl: './audits-list.component.scss',
})
export class AuditsListComponent {
  private readonly service = inject(AuditsService);
  private readonly users = inject(UsersService);
  private readonly notify = inject(NotificationService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);
  /** Backs the audit-type filter dropdown and column labels (lazy-loaded). */
  readonly refLookup = inject(ReferenceDataLookupService);

  readonly auditTypes = this.refLookup.options('audit_type');

  readonly displayedColumns = [
    'name',
    'auditType',
    'status',
    'window',
    'progress',
  ];

  readonly statuses: { value: AuditStatus | 'all'; label: string }[] = [
    { value: 'all', label: 'All statuses' },
    { value: 'draft', label: 'Draft' },
    { value: 'planned', label: 'Planned' },
    { value: 'in_progress', label: 'In progress' },
    { value: 'under_review', label: 'Under review' },
    { value: 'completed', label: 'Completed' },
    { value: 'cancelled', label: 'Cancelled' },
  ];

  readonly filters = this.fb.nonNullable.group({
    status: 'all' as AuditStatus | 'all',
    auditType: '',
  });

  readonly state = signal<ViewState>('loading');
  readonly audits = signal<AuditListItem[]>([]);
  readonly nextCursor = signal<string | null>(null);
  readonly hasMore = signal(false);
  readonly loadingMore = signal(false);
  readonly counts = signal<{ status: string; count: number }[]>([]);

  readonly canCreate = computed(() =>
    this.auth.hasPermission(Permissions.CreateAudit),
  );

  readonly isEmpty = computed(
    () => this.state() === 'ready' && this.audits().length === 0,
  );

  private usersCache: UserDto[] = [];

  constructor() {
    this.loadCounts();
    this.fetchFirstPage();
    this.filters.valueChanges
      .pipe(debounceTime(300), takeUntilDestroyed())
      .subscribe(() => this.fetchFirstPage());
  }

  private loadCounts(): void {
    this.service.counts().subscribe({
      next: (c) =>
        this.counts.set(
          Object.entries(c.byStatus).map(([status, count]) => ({
            status,
            count,
          })),
        ),
      error: () => {
        // Counts are supplementary; leave the summary empty on failure.
      },
    });
  }

  fetchFirstPage(): void {
    this.state.set('loading');
    this.audits.set([]);
    this.nextCursor.set(null);
    this.query(null, (items, cursor, more) => {
      this.audits.set(items);
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
      this.audits.update((current) => [...current, ...items]);
      this.nextCursor.set(cursor);
      this.hasMore.set(more);
      this.loadingMore.set(false);
    });
  }

  private query(
    cursor: string | null,
    onSuccess: (
      items: AuditListItem[],
      cursor: string | null,
      more: boolean,
    ) => void,
  ): void {
    const { status, auditType } = this.filters.getRawValue();
    this.service
      .list({
        status: status === 'all' ? '' : status,
        auditType: auditType.trim() || undefined,
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

  create(): void {
    const openDialog = (users: UserDto[]): void => {
      const data: CreateAuditDialogData = { users };
      this.dialog
        .open(CreateAuditDialogComponent, { data, width: '640px' })
        .afterClosed()
        .subscribe((result?: CreateAuditRequest) => {
          if (!result) {
            return;
          }
          this.service.create(result).subscribe({
            next: (created) => {
              this.notify.success(`Audit "${created.name}" created.`);
              void this.router.navigate(['/audits', created.id]);
            },
          });
        });
    };

    if (this.usersCache.length) {
      openDialog(this.usersCache);
      return;
    }
    this.users.list({ status: 'active', limit: 200 }).subscribe({
      next: (page) => {
        this.usersCache = page.items;
        openDialog(page.items);
      },
      error: () => openDialog([]),
    });
  }

  open(audit: AuditListItem): void {
    void this.router.navigate(['/audits', audit.id]);
  }
}
