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
import { MatListModule } from '@angular/material/list';
import { MatTableModule } from '@angular/material/table';

import { WebhooksService } from '../../../../core/services/webhooks.service';
import { NotificationService } from '../../../../core/services/notification.service';
import { AuthService } from '../../../../core/services/auth.service';
import { Permissions } from '../../../../core/permissions';
import { WebhookSubscription } from '../../../../core/models';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { WebhookSubscriptionDialogComponent } from '../dialogs/webhook-subscription-dialog.component';
import {
  ConfirmDialogComponent,
  ConfirmDialogData,
} from '../../../../shared/components/confirm-dialog/confirm-dialog.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';

type ViewState = 'loading' | 'ready' | 'error';

@Component({
  selector: 'app-webhook-subscriptions',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatListModule,
    TranslatePipe,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
  ],
  templateUrl: './webhook-subscriptions.component.html',
  styleUrl: './webhook-subscriptions.component.scss',
})
export class WebhookSubscriptionsComponent {
  private readonly webhooksService = inject(WebhooksService);
  private readonly notify = inject(NotificationService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly i18n = inject(TranslationService);

  readonly displayedColumns = ['destinationUrl', 'events', 'active', 'actions'];

  readonly state = signal<ViewState>('loading');
  readonly subscriptions = signal<WebhookSubscription[]>([]);

  readonly canManage = computed(() =>
    this.auth.hasPermission(Permissions.ConfigureWebhooks),
  );

  readonly isEmpty = computed(
    () => this.state() === 'ready' && this.subscriptions().length === 0,
  );

  constructor() {
    this.fetch();
  }

  fetch(): void {
    this.state.set('loading');
    this.webhooksService.listSubscriptions().subscribe({
      next: (subs) => {
        this.subscriptions.set(subs);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  create(): void {
    this.dialog
      .open(WebhookSubscriptionDialogComponent, { width: '520px' })
      .afterClosed()
      .subscribe((result) => {
        if (!result) {
          return;
        }
        this.webhooksService.createSubscription(result).subscribe({
          next: (created) => {
            this.notify.success(
              this.i18n.translate('integrations.subscriptions.created.success', {
                url: created.destinationUrl,
              }),
            );
            this.fetch();
          },
          // 409 (external URL rejected) is surfaced by the error interceptor.
        });
      });
  }

  remove(subscription: WebhookSubscription): void {
    const data: ConfirmDialogData = {
      title: this.i18n.translate('integrations.subscriptions.delete.title'),
      message: this.i18n.translate('integrations.subscriptions.delete.message', {
        url: subscription.destinationUrl,
      }),
      confirmLabel: this.i18n.translate('integrations.actions.delete'),
      destructive: true,
    };
    this.dialog
      .open(ConfirmDialogComponent, { data, width: '440px' })
      .afterClosed()
      .subscribe((confirmed) => {
        if (!confirmed) {
          return;
        }
        this.webhooksService.deleteSubscription(subscription.id).subscribe({
          next: () => {
            this.notify.success(this.i18n.translate('integrations.subscriptions.deleted.success'));
            this.fetch();
          },
        });
      });
  }
}
