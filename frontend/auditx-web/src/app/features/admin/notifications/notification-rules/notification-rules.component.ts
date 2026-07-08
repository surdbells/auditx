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
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';

import { NotificationAdminService } from '../../../../core/services/notifications-admin.service';
import { NotificationService } from '../../../../core/services/notification.service';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { AuthService } from '../../../../core/services/auth.service';
import { Permissions } from '../../../../core/permissions';
import { NotificationRule } from '../../../../core/models';
import {
  NotificationRuleDialogComponent,
  NotificationRuleDialogResult,
} from '../dialogs/notification-rule-dialog.component';
import { NotificationPreviewDialogComponent } from '../dialogs/notification-preview-dialog.component';
import {
  ConfirmDialogComponent,
  ConfirmDialogData,
} from '../../../../shared/components/confirm-dialog/confirm-dialog.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';

type ViewState = 'loading' | 'ready' | 'error';

@Component({
  selector: 'app-notification-rules',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    TranslatePipe,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
  ],
  templateUrl: './notification-rules.component.html',
  styleUrl: './notification-rules.component.scss',
})
export class NotificationRulesComponent {
  private readonly notifications = inject(NotificationAdminService);
  private readonly notify = inject(NotificationService);
  private readonly i18n = inject(TranslationService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);

  readonly displayedColumns = [
    'eventType',
    'name',
    'channels',
    'recipients',
    'active',
    'actions',
  ];

  readonly state = signal<ViewState>('loading');
  readonly rules = signal<NotificationRule[]>([]);
  private readonly eventTypes = signal<string[]>([]);

  readonly canManage = computed(() =>
    this.auth.hasPermission(Permissions.ConfigureNotifications),
  );

  readonly isEmpty = computed(
    () => this.state() === 'ready' && this.rules().length === 0,
  );

  constructor() {
    this.fetch();
    this.notifications.eventCatalogue().subscribe({
      next: (types) => this.eventTypes.set(types),
      // Catalogue is best-effort; the dialog falls back to a free-text input.
      error: () => this.eventTypes.set([]),
    });
  }

  fetch(): void {
    this.state.set('loading');
    this.notifications.listRules().subscribe({
      next: (rules) => {
        this.rules.set(rules);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  /** Summarise a channels/recipient JSON blob for compact table display. */
  summarise(json: string): string {
    if (!json) {
      return '—';
    }
    try {
      const parsed: unknown = JSON.parse(json);
      if (Array.isArray(parsed)) {
        return parsed.map((v) => String(v)).join(', ') || '—';
      }
      if (parsed && typeof parsed === 'object') {
        const record = parsed as Record<string, unknown>;
        const type = record['type'];
        const value = record['value'];
        if (type !== undefined) {
          return value !== undefined ? `${type}: ${value}` : String(type);
        }
        return Object.values(record)
          .map((v) => String(v))
          .join(', ');
      }
      return String(parsed);
    } catch {
      return json;
    }
  }

  create(): void {
    this.dialog
      .open(NotificationRuleDialogComponent, {
        width: '560px',
        data: { eventTypes: this.eventTypes() },
      })
      .afterClosed()
      .subscribe((result: NotificationRuleDialogResult | undefined) => {
        if (!result || result.mode !== 'create') {
          return;
        }
        this.notifications.createRule(result.body).subscribe({
          next: (created) => {
            this.notify.success(
              this.i18n.translate('notifications.rules.toast.created', {
                name: created.name,
              }),
            );
            this.fetch();
          },
        });
      });
  }

  edit(rule: NotificationRule): void {
    this.dialog
      .open(NotificationRuleDialogComponent, {
        width: '560px',
        data: { rule, eventTypes: this.eventTypes() },
      })
      .afterClosed()
      .subscribe((result: NotificationRuleDialogResult | undefined) => {
        if (!result || result.mode !== 'update') {
          return;
        }
        this.notifications.updateRule(result.id, result.body).subscribe({
          next: (updated) => {
            this.notify.success(
              this.i18n.translate('notifications.rules.toast.updated', {
                name: updated.name,
              }),
            );
            this.fetch();
          },
          // 409 (concurrent edit) is surfaced by the error interceptor.
        });
      });
  }

  deactivate(rule: NotificationRule): void {
    const data: ConfirmDialogData = {
      title: this.i18n.translate('notifications.rules.deactivate.title'),
      message: this.i18n.translate('notifications.rules.deactivate.message', {
        name: rule.name,
      }),
      confirmLabel: this.i18n.translate('notifications.actions.deactivate'),
      destructive: true,
    };
    this.dialog
      .open(ConfirmDialogComponent, { data, width: '440px' })
      .afterClosed()
      .subscribe((confirmed) => {
        if (!confirmed) {
          return;
        }
        this.notifications.deactivateRule(rule.id).subscribe({
          next: () => {
            this.notify.success(
              this.i18n.translate('notifications.rules.toast.deactivated'),
            );
            this.fetch();
          },
        });
      });
  }

  preview(rule: NotificationRule): void {
    this.dialog.open(NotificationPreviewDialogComponent, {
      width: '620px',
      data: { rule },
    });
  }
}
