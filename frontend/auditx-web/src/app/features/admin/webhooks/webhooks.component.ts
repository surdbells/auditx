import { ChangeDetectionStrategy, Component } from '@angular/core';
import { MatTabsModule } from '@angular/material/tabs';

import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { WebhookSubscriptionsComponent } from './webhook-subscriptions/webhook-subscriptions.component';
import { WebhookDeliveriesComponent } from './webhook-deliveries/webhook-deliveries.component';

@Component({
  selector: 'app-webhooks',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatTabsModule,
    PageHeaderComponent,
    WebhookSubscriptionsComponent,
    WebhookDeliveriesComponent,
  ],
  template: `
    <app-page-header
      title="Webhooks"
      subtitle="Outbound event subscriptions and their delivery history."
    />
    <mat-tab-group>
      <mat-tab label="Subscriptions">
        <div class="tab-body">
          <app-webhook-subscriptions />
        </div>
      </mat-tab>
      <mat-tab label="Deliveries">
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
