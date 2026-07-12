import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { IconComponent } from '../../../../core/icons/icon.component';
import { MatMenuModule } from '@angular/material/menu';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatTableModule } from '@angular/material/table';
import { MatTooltipModule } from '@angular/material/tooltip';

import { RolesService } from '../../../../core/services/roles.service';
import { NotificationService } from '../../../../core/services/notification.service';
import { RoleDto } from '../../../../core/models';
import {
  ConfirmDialogComponent,
  ConfirmDialogData,
} from '../../../../shared/components/confirm-dialog/confirm-dialog.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { PageGuideComponent } from '../../../../shared/components/page-guide/page-guide.component';
import { PageGuide } from '../../../../core/models/page-guide.models';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';

type ViewState = 'loading' | 'ready' | 'error';

/** Contextual page guide for the roles admin list (drives the walkthrough + the About panel). */
const ROLES_GUIDE: PageGuide = {
  id: 'roles-list',
  titleKey: 'identity.roles.title',
  purposeKey: 'roles.guide.purpose',
  descriptionKey: 'roles.guide.description',
  actionKeys: [
    'roles.guide.action.create',
    'roles.guide.action.edit',
    'roles.guide.action.archive',
    'roles.guide.action.showArchived',
  ],
  sections: [
    { selector: '[data-guide="create"]', titleKey: 'roles.guide.section.create.title', bodyKey: 'roles.guide.section.create.body' },
    { selector: '.roles__toolbar', titleKey: 'roles.guide.section.toolbar.title', bodyKey: 'roles.guide.section.toolbar.body' },
    { selector: '.roles__table', titleKey: 'roles.guide.section.table.title', bodyKey: 'roles.guide.section.table.body' },
  ],
  workflowKeys: ['roles.guide.flow.define', 'roles.guide.flow.permissions', 'roles.guide.flow.inherit', 'roles.guide.flow.assign', 'roles.guide.flow.enforce'],
  dependsOnKeys: ['roles.guide.dep.permissions', 'roles.guide.dep.builtin', 'roles.guide.dep.parents'],
  usedByKeys: ['roles.guide.use.users', 'roles.guide.use.audits', 'roles.guide.use.access'],
  businessRuleKeys: ['roles.guide.rule.builtin', 'roles.guide.rule.inherit', 'roles.guide.rule.archive', 'roles.guide.rule.assigned'],
  tipKeys: ['roles.guide.tip.inherit', 'roles.guide.tip.least', 'roles.guide.tip.showArchived'],
  permissionKeys: ['roles.guide.perm.admin', 'roles.guide.perm.viewer'],
  faq: [
    { questionKey: 'roles.guide.faq.builtin.q', answerKey: 'roles.guide.faq.builtin.a' },
    { questionKey: 'roles.guide.faq.inherit.q', answerKey: 'roles.guide.faq.inherit.a' },
  ],
};

@Component({
  selector: 'app-roles-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    IconComponent,
    MatMenuModule,
    MatChipsModule,
    MatSlideToggleModule,
    MatTooltipModule,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PageHeaderComponent,
    PageGuideComponent,
    TranslatePipe,
  ],
  templateUrl: './roles-list.component.html',
  styleUrl: './roles-list.component.scss',
})
export class RolesListComponent {
  private readonly rolesService = inject(RolesService);
  private readonly notify = inject(NotificationService);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);
  private readonly i18n = inject(TranslationService);

  readonly guide = ROLES_GUIDE;

  readonly displayedColumns = [
    'name',
    'description',
    'permissions',
    'flags',
    'actions',
  ];

  readonly state = signal<ViewState>('loading');
  readonly roles = signal<RoleDto[]>([]);
  readonly includeArchived = signal(false);

  readonly isEmpty = computed(
    () => this.state() === 'ready' && this.roles().length === 0,
  );

  constructor() {
    this.fetch();
  }

  fetch(): void {
    this.state.set('loading');
    this.rolesService.list(this.includeArchived()).subscribe({
      next: (roles) => {
        this.roles.set(roles);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  toggleArchived(checked: boolean): void {
    this.includeArchived.set(checked);
    this.fetch();
  }

  createRole(): void {
    void this.router.navigate(['/admin/roles/new']);
  }

  editRole(role: RoleDto): void {
    void this.router.navigate(['/admin/roles', role.id]);
  }

  archive(role: RoleDto, event: Event): void {
    event.stopPropagation();
    const data: ConfirmDialogData = {
      title: this.i18n.translate('identity.roles.archive.title'),
      message: this.i18n.translate('identity.roles.archive.message', {
        name: role.name,
      }),
      confirmLabel: this.i18n.translate('identity.actions.archive'),
      destructive: true,
    };
    this.dialog
      .open(ConfirmDialogComponent, { data, width: '440px' })
      .afterClosed()
      .subscribe((confirmed) => {
        if (!confirmed) {
          return;
        }
        this.rolesService.archive(role.id).subscribe({
          next: () => {
            this.notify.success(
              this.i18n.translate('identity.roles.archive.success', {
                name: role.name,
              }),
            );
            this.fetch();
          },
        });
      });
  }
}
