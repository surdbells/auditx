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
import { PaginatorComponent } from '../../../../shared/components/paginator/paginator.component';
import { UserStatusLabelPipe } from '../../../../shared/pipes/user-status-label.pipe';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { PageGuideComponent } from '../../../../shared/components/page-guide/page-guide.component';
import { PageGuide } from '../../../../core/models/page-guide.models';

type ViewState = 'loading' | 'ready' | 'error';

const DEFAULT_PAGE_SIZE = 25;

/** Contextual page guide for the users identity list (drives the walkthrough + the About panel). */
const USERS_GUIDE: PageGuide = {
  id: 'users-list',
  titleKey: 'identity.users.title',
  purposeKey: 'identity.guide.purpose',
  descriptionKey: 'identity.guide.description',
  actionKeys: [
    'identity.guide.action.search',
    'identity.guide.action.filter',
    'identity.guide.action.open',
    'identity.guide.action.deactivate',
  ],
  sections: [
    { selector: '.users__filters-card', titleKey: 'identity.guide.section.filters.title', bodyKey: 'identity.guide.section.filters.body' },
    { selector: '.users__table', titleKey: 'identity.guide.section.table.title', bodyKey: 'identity.guide.section.table.body' },
  ],
  workflowKeys: ['identity.guide.flow.provision', 'identity.guide.flow.directory', 'identity.guide.flow.role', 'identity.guide.flow.active'],
  dependsOnKeys: ['identity.guide.dep.ad', 'identity.guide.dep.roles', 'identity.guide.dep.orgunit'],
  usedByKeys: ['identity.guide.use.audits', 'identity.guide.use.assignments', 'identity.guide.use.auditlog'],
  businessRuleKeys: ['identity.guide.rule.provisioned', 'identity.guide.rule.awaiting', 'identity.guide.rule.deactivate'],
  tipKeys: ['identity.guide.tip.search', 'identity.guide.tip.awaiting'],
  permissionKeys: ['identity.guide.perm.admin', 'identity.guide.perm.viewer'],
  faq: [
    { questionKey: 'identity.guide.faq.create.q', answerKey: 'identity.guide.faq.create.a' },
    { questionKey: 'identity.guide.faq.status.q', answerKey: 'identity.guide.faq.status.a' },
  ],
};

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
    PaginatorComponent,
    UserStatusLabelPipe,
    TranslatePipe,
    PageGuideComponent,
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
  private readonly i18n = inject(TranslationService);

  readonly guide = USERS_GUIDE;

  readonly displayedColumns = [
    'displayName',
    'email',
    'roles',
    'status',
    'lastLoginAt',
    'actions',
  ];

  readonly statuses: { value: UserStatus | ''; labelKey: string }[] = [
    { value: '', labelKey: 'identity.users.status.all' },
    { value: 'active', labelKey: 'identity.status.active' },
    { value: 'deactivated', labelKey: 'identity.status.deactivated' },
    { value: 'awaiting_role_assignment', labelKey: 'identity.status.awaitingRole' },
    { value: 'locked', labelKey: 'identity.status.locked' },
  ];

  readonly filters = this.fb.nonNullable.group({
    search: '',
    role: '',
    status: '' as UserStatus | '',
  });

  readonly state = signal<ViewState>('loading');
  readonly users = signal<UserDto[]>([]);
  readonly total = signal(0);
  readonly page = signal(1);
  readonly pageSize = signal(DEFAULT_PAGE_SIZE);
  /** In-flight fetch (page navigation / filter change) — disables the paginator without clearing the table. */
  readonly loading = signal(false);
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
    this.fetchPage(1);

    // React to debounced filter changes after the initial load.
    this.filters.valueChanges
      .pipe(debounceTime(300), takeUntilDestroyed())
      .subscribe(() => this.fetchPage(1));
    // Reference signal so it is retained (used for change detection consistency).
    void this.filterValues;
  }

  private loadRoles(): void {
    this.rolesService.list(false).subscribe({
      next: (roles) => this.roles.set(roles),
      error: () => this.roles.set([]),
    });
  }

  fetchPage(page: number): void {
    this.loading.set(true);
    const { search, role, status } = this.filters.getRawValue();
    this.usersService
      .list({ search, role, status, page, pageSize: this.pageSize() })
      .subscribe({
        next: (result) => {
          this.users.set(result.items);
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

  clearFilters(): void {
    this.filters.reset({ search: '', role: '', status: '' });
  }

  openUser(user: UserDto): void {
    void this.router.navigate(['/admin/users', user.id]);
  }

  deactivate(user: UserDto, event: Event): void {
    event.stopPropagation();
    const data: ConfirmDialogData = {
      title: this.i18n.translate('identity.users.deactivate.title'),
      message: this.i18n.translate('identity.users.deactivate.message', {
        name: user.displayName,
      }),
      confirmLabel: this.i18n.translate('identity.actions.deactivate'),
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
            this.notify.success(
              this.i18n.translate('identity.users.deactivate.success', {
                name: user.displayName,
              }),
            );
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
