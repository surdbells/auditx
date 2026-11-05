import { ChangeDetectionStrategy, Component } from '@angular/core';
import { MatTabsModule } from '@angular/material/tabs';

import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { PageGuideComponent } from '../../../shared/components/page-guide/page-guide.component';
import { PageGuide } from '../../../core/models/page-guide.models';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { WebhookSubscriptionsComponent } from './webhook-subscriptions/webhook-subscriptions.component';
import { WebhookDeliveriesComponent } from './webhook-deliveries/webhook-deliveries.component';

/** Contextual page guide for the webhooks admin page. */
const WEBHOOKS_GUIDE: PageGuide = {
  id: 'admin-webhooks',
  titleKey: 'integrations.webhooks.title',
  purposeKey: 'integrations.webhooks.guide.purpose',
  descriptionKey: 'integrations.webhooks.guide.description',
  actionKeys: [
    'integrations.webhooks.guide.action.subscribe',
    'integrations.webhooks.guide.action.deliveries',
    'integrations.webhooks.guide.action.retry',
  ],
  tipKeys: [
    'integrations.webhooks.guide.tip.secret',
    'integrations.webhooks.guide.tip.retries',
  ],
};

@Component({
  selector: 'app-webhooks',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatTabsModule,
    PageHeaderComponent,
    PageGuideComponent,
    TranslatePipe,
    WebhookSubscriptionsComponent,
    WebhookDeliveriesComponent,
  ],
  template: `
    <app-page-header
      [title]="'integrations.webhooks.title' | t"
      [subtitle]="'integrations.webhooks.subtitle' | t"
    />
    <app-page-guide [guide]="guide" />
    <mat-tab-group class="ax-tabs">
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
export class WebhooksComponent {
  readonly guide = WEBHOOKS_GUIDE;
}
