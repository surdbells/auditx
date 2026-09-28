import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { IconComponent } from '../../../../core/icons/icon.component';
import { MatTableModule } from '@angular/material/table';

import { NotificationAdminService } from '../../../../core/services/notifications-admin.service';
import { NotificationService } from '../../../../core/services/notification.service';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { AuthService } from '../../../../core/services/auth.service';
import { Permissions } from '../../../../core/permissions';
import { NotificationTemplate } from '../../../../core/models';
import { NotificationTemplateDialogComponent } from '../dialogs/notification-template-dialog.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';

type ViewState = 'loading' | 'ready' | 'error';

@Component({
  selector: 'app-notification-templates',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    IconComponent,
    TranslatePipe,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
  ],
  templateUrl: './notification-templates.component.html',
  styleUrl: './notification-templates.component.scss',
})
export class NotificationTemplatesComponent {
  private readonly notifications = inject(NotificationAdminService);
  private readonly notify = inject(NotificationService);
  private readonly i18n = inject(TranslationService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);

  readonly displayedColumns = [
    'templateKey',
    'channel',
    'scope',
    'subject',
    'actions',
  ];

  readonly state = signal<ViewState>('loading');
  readonly templates = signal<NotificationTemplate[]>([]);

  readonly canManage = computed(() =>
    this.auth.hasPermission(Permissions.ConfigureNotifications),
  );

  readonly isEmpty = computed(
    () => this.state() === 'ready' && this.templates().length === 0,
  );

  constructor() {
    this.fetch();
  }

  fetch(): void {
    this.state.set('loading');
    this.notifications.listTemplates().subscribe({
      next: (templates) => {
        this.templates.set(templates);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  override(template: NotificationTemplate): void {
    this.dialog
      .open(NotificationTemplateDialogComponent, {
        width: '560px',
        data: { template },
      })
      .afterClosed()
      .subscribe((result) => {
        if (!result) {
          return;
        }
        this.notifications.overrideTemplate(result).subscribe({
          next: (saved) => {
            this.notify.success(
              this.i18n.translate(
                'notifications.templates.toast.overrideSaved',
                { key: saved.templateKey },
              ),
            );
            this.fetch();
          },
        });
      });
  }

  createInstitutionTemplate(): void {
    this.dialog
      .open(NotificationTemplateDialogComponent, {
        width: '560px',
        data: {},
      })
      .afterClosed()
      .subscribe((result) => {
        if (!result) {
          return;
        }
        this.notifications.overrideTemplate(result).subscribe({
          next: (saved) => {
            this.notify.success(
              this.i18n.translate('notifications.templates.toast.created', {
                key: saved.templateKey,
              }),
            );
            this.fetch();
          },
        });
      });
  }
}
