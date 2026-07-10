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
import { PageGuideComponent } from '../../../../shared/components/page-guide/page-guide.component';
import { PageGuide } from '../../../../core/models/page-guide.models';
import { UserStatusLabelPipe } from '../../../../shared/pipes/user-status-label.pipe';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import {
  GrantRoleDialogComponent,
  GrantRoleDialogData,
} from '../dialogs/grant-role-dialog.component';
import {
  CreateDelegationDialogComponent,
  CreateDelegationDialogData,
} from '../dialogs/create-delegation-dialog.component';
import {
  SetCapacityDialogComponent,
  SetCapacityDialogData,
} from '../dialogs/set-capacity-dialog.component';

type ViewState = 'loading' | 'ready' | 'error';

/** Contextual page guide for the user-detail page (drives the walkthrough + the About panel). */
const USER_DETAIL_GUIDE: PageGuide = {
  id: 'identity-user-detail',
  titleKey: 'identity.userDetail.guide.pageTitle',
  purposeKey: 'identity.userDetail.guide.purpose',
  descriptionKey: 'identity.userDetail.guide.description',
  actionKeys: [
    'identity.userDetail.guide.action.grant',
    'identity.userDetail.guide.action.revoke',
    'identity.userDetail.guide.action.delegate',
    'identity.userDetail.guide.action.back',
  ],
  sections: [
    { selector: '.detail__profile', titleKey: 'identity.userDetail.guide.section.profile.title', bodyKey: 'identity.userDetail.guide.section.profile.body' },
    { selector: '.detail__roles', titleKey: 'identity.userDetail.guide.section.roles.title', bodyKey: 'identity.userDetail.guide.section.roles.body' },
    { selector: '.detail__delegations', titleKey: 'identity.userDetail.guide.section.delegations.title', bodyKey: 'identity.userDetail.guide.section.delegations.body' },
  ],
  workflowKeys: [
    'identity.userDetail.guide.flow.create',
    'identity.userDetail.guide.flow.grant',
    'identity.userDetail.guide.flow.delegate',
    'identity.userDetail.guide.flow.access',
  ],
  dependsOnKeys: [
    'identity.userDetail.guide.dep.users',
    'identity.userDetail.guide.dep.roles',
    'identity.userDetail.guide.dep.scopes',
  ],
  usedByKeys: [
    'identity.userDetail.guide.use.audits',
    'identity.userDetail.guide.use.approvals',
    'identity.userDetail.guide.use.audittrail',
  ],
  businessRuleKeys: [
    'identity.userDetail.guide.rule.scope',
    'identity.userDetail.guide.rule.delegationWindow',
    'identity.userDetail.guide.rule.status',
    'identity.userDetail.guide.rule.selfService',
  ],
  tipKeys: [
    'identity.userDetail.guide.tip.scope',
    'identity.userDetail.guide.tip.delegation',
    'identity.userDetail.guide.tip.review',
  ],
  permissionKeys: [
    'identity.userDetail.guide.perm.admin',
    'identity.userDetail.guide.perm.manager',
  ],
  faq: [
    { questionKey: 'identity.userDetail.guide.faq.scope.q', answerKey: 'identity.userDetail.guide.faq.scope.a' },
    { questionKey: 'identity.userDetail.guide.faq.delegation.q', answerKey: 'identity.userDetail.guide.faq.delegation.a' },
  ],
};

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
    PageGuideComponent,
    UserStatusLabelPipe,
    TranslatePipe,
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
  private readonly i18n = inject(TranslationService);

  readonly guide = USER_DETAIL_GUIDE;

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

  openSetCapacity(): void {
    const current = this.user();
    if (!current) {
      return;
    }
    const data: SetCapacityDialogData = {
      userDisplayName: current.displayName,
      capacityDays: current.capacityDays,
    };
    this.dialog
      .open(SetCapacityDialogComponent, { data })
      .afterClosed()
      .subscribe((result) => {
        if (!result) {
          return;
        }
        this.usersService.setCapacity(current.id, result.capacityDays).subscribe({
          next: () => {
            this.notify.success(this.i18n.translate('identity.capacity.saved'));
            this.fetch();
          },
        });
      });
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
            this.notify.success(this.i18n.translate('identity.userDetail.roleGranted'));
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
      title: this.i18n.translate('identity.userDetail.revokeRole'),
      message: this.i18n.translate('identity.userDetail.revokeRoleMessage', {
        role: role.roleName,
        name: current.displayName,
      }),
      confirmLabel: this.i18n.translate('identity.actions.revoke'),
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
            this.notify.success(this.i18n.translate('identity.userDetail.roleRevoked'));
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
            this.notify.success(this.i18n.translate('identity.userDetail.delegationCreated'));
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
      title: this.i18n.translate('identity.userDetail.revokeDelegation'),
      message: this.i18n.translate('identity.userDetail.revokeDelegationMessage', {
        role: delegation.roleName,
      }),
      confirmLabel: this.i18n.translate('identity.actions.revoke'),
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
              this.notify.success(this.i18n.translate('identity.userDetail.delegationRevoked'));
              this.fetch();
            },
          });
      });
  }
}
