import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import {
  FormBuilder,
  ReactiveFormsModule,
} from '@angular/forms';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatMenuModule } from '@angular/material/menu';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';
import { MatDialog } from '@angular/material/dialog';
import { debounceTime, distinctUntilChanged } from 'rxjs';

import { UsersService } from '../../../../core/services/users.service';
import { RolesService } from '../../../../core/services/roles.service';
import { NotificationService } from '../../../../core/services/notification.service';
import { RoleDto, UserDto, UserStatus } from '../../../../core/models';
import {
  ConfirmDialogComponent,
  ConfirmDialogData,
} from '../../../../shared/components/confirm-dialog/confirm-dialog.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { UserStatusLabelPipe } from '../../../../shared/pipes/user-status-label.pipe';

type ViewState = 'loading' | 'ready' | 'error';

const PAGE_SIZE = 20;

@Component({
  selector: 'app-users-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    RouterLink,
    ReactiveFormsModule,
    MatCardModule,
    MatTableModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatChipsModule,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PageHeaderComponent,
    UserStatusLabelPipe,
  ],
  templateUrl: './users-list.component.html',
  styleUrl: './users-list.component.scss',
})
export class UsersListComponent {
  private readonly usersService = inject(UsersService);
  private readonly rolesService = inject(RolesService);
  private readonly notify = inject(NotificationService);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);

  readonly displayedColumns = [
    'displayName',
    'email',
    'status',
    'lastLoginAt',
    'actions',
  ];

  readonly statuses: { value: UserStatus | ''; label: string }[] = [
    { value: '', label: 'All statuses' },
    { value: 'active', label: 'Active' },
    { value: 'deactivated', label: 'Deactivated' },
    { value: 'awaiting_role_assignment', label: 'Awaiting role' },
    { value: 'locked', label: 'Locked' },
  ];

  readonly filters = this.fb.nonNullable.group({
    search: '',
    role: '',
    status: '' as UserStatus | '',
  });

  readonly state = signal<ViewState>('loading');
  readonly users = signal<UserDto[]>([]);
  readonly nextCursor = signal<string | null>(null);
  readonly hasMore = signal(false);
  readonly loadingMore = signal(false);
  readonly roles = signal<RoleDto[]>([]);

  readonly isEmpty = computed(
    () => this.state() === 'ready' && this.users().length === 0,
  );

  /** Reactively re-fetch the first page whenever filters change. */
  private readonly filterValues = toSignal(
    this.filters.valueChanges.pipe(
      debounceTime(300),
      distinctUntilChanged(
        (a, b) => JSON.stringify(a) === JSON.stringify(b),
      ),
    ),
  );

  constructor() {
    this.loadRoles();
    this.fetchFirstPage();

    // React to debounced filter changes after the initial load.
    this.filters.valueChanges
      .pipe(debounceTime(300), takeUntilDestroyed())
      .subscribe(() => this.fetchFirstPage());
    // Reference signal so it is retained (used for change detection consistency).
    void this.filterValues;
  }

  private loadRoles(): void {
    this.rolesService.list(false).subscribe({
      next: (roles) => this.roles.set(roles),
      error: () => this.roles.set([]),
    });
  }

  fetchFirstPage(): void {
    this.state.set('loading');
    this.users.set([]);
    this.nextCursor.set(null);
    this.query(null, (items, cursor, more) => {
      this.users.set(items);
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
      this.users.update((current) => [...current, ...items]);
      this.nextCursor.set(cursor);
      this.hasMore.set(more);
      this.loadingMore.set(false);
    });
  }

  private query(
    cursor: string | null,
    onSuccess: (items: UserDto[], cursor: string | null, more: boolean) => void,
  ): void {
    const { search, role, status } = this.filters.getRawValue();
    this.usersService
      .list({ search, role, status, cursor, limit: PAGE_SIZE })
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

  clearFilters(): void {
    this.filters.reset({ search: '', role: '', status: '' });
  }

  openUser(user: UserDto): void {
    void this.router.navigate(['/admin/users', user.id]);
  }

  deactivate(user: UserDto, event: Event): void {
    event.stopPropagation();
    const data: ConfirmDialogData = {
      title: 'Deactivate user',
      message: `Deactivate ${user.displayName}? They will immediately lose access to AuditX.`,
      confirmLabel: 'Deactivate',
      destructive: true,
    };
    this.dialog
      .open(ConfirmDialogComponent, { data, width: '420px' })
      .afterClosed()
      .subscribe((confirmed) => {
        if (!confirmed) {
          return;
        }
        this.usersService.deactivate(user.id).subscribe({
          next: () => {
            this.notify.success(`${user.displayName} has been deactivated.`);
            this.users.update((list) =>
              list.map((u) =>
                u.id === user.id ? { ...u, status: 'deactivated' } : u,
              ),
            );
          },
        });
      });
  }
}
