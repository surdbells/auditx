import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatDividerModule } from '@angular/material/divider';
import { MatIconModule } from '@angular/material/icon';
import { MatListModule } from '@angular/material/list';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';

import { UsersService } from '../../../../core/services/users.service';
import { RolesService } from '../../../../core/services/roles.service';
import { NotificationService } from '../../../../core/services/notification.service';
import {
  DelegationDto,
  RoleDto,
  UserDetailDto,
  UserRoleDto,
} from '../../../../core/models';
import {
  ConfirmDialogComponent,
  ConfirmDialogData,
} from '../../../../shared/components/confirm-dialog/confirm-dialog.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { UserStatusLabelPipe } from '../../../../shared/pipes/user-status-label.pipe';
import {
  GrantRoleDialogComponent,
  GrantRoleDialogData,
} from '../dialogs/grant-role-dialog.component';
import {
  CreateDelegationDialogComponent,
  CreateDelegationDialogData,
} from '../dialogs/create-delegation-dialog.component';

type ViewState = 'loading' | 'ready' | 'error';

@Component({
  selector: 'app-user-detail',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatChipsModule,
    MatDividerModule,
    MatListModule,
    MatTableModule,
    MatTooltipModule,
    LoadingComponent,
    ErrorStateComponent,
    PageHeaderComponent,
    UserStatusLabelPipe,
  ],
  templateUrl: './user-detail.component.html',
  styleUrl: './user-detail.component.scss',
})
export class UserDetailComponent {
  /** Route param bound via withComponentInputBinding(). */
  readonly id = input.required<string>();

  private readonly usersService = inject(UsersService);
  private readonly rolesService = inject(RolesService);
  private readonly notify = inject(NotificationService);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);

  readonly state = signal<ViewState>('loading');
  readonly user = signal<UserDetailDto | null>(null);
  readonly roles = signal<RoleDto[]>([]);

  readonly directRoles = computed(() =>
    (this.user()?.roles ?? []).filter((r) => !r.isDelegation),
  );

  readonly delegations = computed(() => this.user()?.delegations ?? []);

  readonly delegationColumns = ['roleName', 'period', 'status', 'actions'];

  constructor() {
    // input() is set synchronously before constructor completes for route inputs;
    // fetch when the component is created.
    queueMicrotask(() => {
      this.loadRoles();
      this.fetch();
    });
  }

  fetch(): void {
    this.state.set('loading');
    this.usersService.getById(this.id()).subscribe({
      next: (user) => {
        this.user.set(user);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  private loadRoles(): void {
    this.rolesService.list(false).subscribe({
      next: (roles) => this.roles.set(roles),
      error: () => this.roles.set([]),
    });
  }

  goBack(): void {
    void this.router.navigate(['/admin/users']);
  }

  openGrantRole(): void {
    const current = this.user();
    if (!current) {
      return;
    }
    const data: GrantRoleDialogData = {
      userDisplayName: current.displayName,
      roles: this.roles(),
    };
    this.dialog
      .open(GrantRoleDialogComponent, { data })
      .afterClosed()
      .subscribe((request) => {
        if (!request) {
          return;
        }
        this.usersService.grantRole(current.id, request).subscribe({
          next: () => {
            this.notify.success('Role granted.');
            this.fetch();
          },
        });
      });
  }

  revokeRole(role: UserRoleDto): void {
    const current = this.user();
    if (!current) {
      return;
    }
    const data: ConfirmDialogData = {
      title: 'Revoke role',
      message: `Revoke the "${role.roleName}" role from ${current.displayName}?`,
      confirmLabel: 'Revoke',
      destructive: true,
    };
    this.dialog
      .open(ConfirmDialogComponent, { data, width: '420px' })
      .afterClosed()
      .subscribe((confirmed) => {
        if (!confirmed) {
          return;
        }
        this.usersService.revokeRole(current.id, role.id).subscribe({
          next: () => {
            this.notify.success('Role revoked.');
            this.fetch();
          },
        });
      });
  }

  openCreateDelegation(): void {
    const current = this.user();
    if (!current) {
      return;
    }
    const data: CreateDelegationDialogData = {
      fromUserDisplayName: current.displayName,
      roles: this.roles(),
    };
    this.dialog
      .open(CreateDelegationDialogComponent, { data })
      .afterClosed()
      .subscribe((request) => {
        if (!request) {
          return;
        }
        this.usersService.createDelegation(current.id, request).subscribe({
          next: () => {
            this.notify.success('Delegation created.');
            this.fetch();
          },
        });
      });
  }

  revokeDelegation(delegation: DelegationDto): void {
    const current = this.user();
    if (!current) {
      return;
    }
    const data: ConfirmDialogData = {
      title: 'Revoke delegation',
      message: `Revoke the delegated "${delegation.roleName}" role?`,
      confirmLabel: 'Revoke',
      destructive: true,
    };
    this.dialog
      .open(ConfirmDialogComponent, { data, width: '420px' })
      .afterClosed()
      .subscribe((confirmed) => {
        if (!confirmed) {
          return;
        }
        this.usersService
          .revokeDelegation(current.id, delegation.id)
          .subscribe({
            next: () => {
              this.notify.success('Delegation revoked.');
              this.fetch();
            },
          });
      });
  }
}
