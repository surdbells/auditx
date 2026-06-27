import { ChangeDetectionStrategy, Component } from '@angular/core';
import { MatTabsModule } from '@angular/material/tabs';

import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { NotificationRulesComponent } from './notification-rules/notification-rules.component';
import { NotificationTemplatesComponent } from './notification-templates/notification-templates.component';
import { NotificationDispatchesComponent } from './notification-dispatches/notification-dispatches.component';
import { NotificationDeadLetterComponent } from './notification-dead-letter/notification-dead-letter.component';

@Component({
  selector: 'app-notifications',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatTabsModule,
    PageHeaderComponent,
    NotificationRulesComponent,
    NotificationTemplatesComponent,
    NotificationDispatchesComponent,
    NotificationDeadLetterComponent,
  ],
  template: `
    <app-page-header
      title="Notifications"
      subtitle="Event-driven notification rules, templates and delivery history."
    />
    <mat-tab-group>
      <mat-tab label="Rules">
        <div class="tab-body">
          <app-notification-rules />
        </div>
      </mat-tab>
      <mat-tab label="Templates">
        <div class="tab-body">
          <app-notification-templates />
        </div>
      </mat-tab>
      <mat-tab label="Dispatch Log">
        <div class="tab-body">
          <app-notification-dispatches />
        </div>
      </mat-tab>
      <mat-tab label="Dead Letter">
        <div class="tab-body">
          <app-notification-dead-letter />
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
export class NotificationsComponent {}
