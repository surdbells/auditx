import { ChangeDetectionStrategy, Component } from '@angular/core';
import { MatTabsModule } from '@angular/material/tabs';

import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { WebhookSubscriptionsComponent } from './webhook-subscriptions/webhook-subscriptions.component';
import { WebhookDeliveriesComponent } from './webhook-deliveries/webhook-deliveries.component';

@Component({
  selector: 'app-webhooks',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatTabsModule,
    PageHeaderComponent,
    TranslatePipe,
    WebhookSubscriptionsComponent,
    WebhookDeliveriesComponent,
  ],
  template: `
    <app-page-header
      [title]="'integrations.webhooks.title' | t"
      [subtitle]="'integrations.webhooks.subtitle' | t"
    />
    <mat-tab-group>
      <mat-tab [label]="'integrations.webhooks.tab.subscriptions' | t">
        <div class="tab-body">
          <app-webhook-subscriptions />
        </div>
      </mat-tab>
      <mat-tab [label]="'integrations.webhooks.tab.deliveries' | t">
        <div class="tab-body">
          <app-webhook-deliveries />
        </div>
      </mat-tab>
    </mat-tab-group>
  `,
  styles: `
    .tab-body {
      padding-top: 1.5rem;
    }
  `,
})
export class WebhooksComponent {}
