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
import { MatIconModule } from '@angular/material/icon';
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

type ViewState = 'loading' | 'ready' | 'error';

@Component({
  selector: 'app-roles-list',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    RouterLink,
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatChipsModule,
    MatSlideToggleModule,
    MatTooltipModule,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PageHeaderComponent,
  ],
  templateUrl: './roles-list.component.html',
  styleUrl: './roles-list.component.scss',
})
export class RolesListComponent {
  private readonly rolesService = inject(RolesService);
  private readonly notify = inject(NotificationService);
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);

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
      title: 'Archive role',
      message: `Archive the "${role.name}" role? It will no longer be assignable. This may require maker-checker approval.`,
      confirmLabel: 'Archive',
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
            this.notify.success(`"${role.name}" archived.`);
            this.fetch();
          },
        });
      });
  }
}
