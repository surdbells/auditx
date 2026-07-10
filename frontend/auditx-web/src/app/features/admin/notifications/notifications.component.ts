import { ChangeDetectionStrategy, Component } from '@angular/core';
import { MatTabsModule } from '@angular/material/tabs';

import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { PageGuideComponent } from '../../../shared/components/page-guide/page-guide.component';
import { PageGuide } from '../../../core/models/page-guide.models';
import { NotificationRulesComponent } from './notification-rules/notification-rules.component';
import { NotificationTemplatesComponent } from './notification-templates/notification-templates.component';
import { NotificationDispatchesComponent } from './notification-dispatches/notification-dispatches.component';
import { NotificationDeadLetterComponent } from './notification-dead-letter/notification-dead-letter.component';

/** Contextual page guide for the notifications admin (drives the walkthrough + the About panel). */
const NOTIFICATIONS_GUIDE: PageGuide = {
  id: 'admin-notifications',
  titleKey: 'notifications.page.title',
  purposeKey: 'notifications.guide.purpose',
  descriptionKey: 'notifications.guide.description',
  actionKeys: [
    'notifications.guide.action.configure',
    'notifications.guide.action.templates',
    'notifications.guide.action.monitor',
    'notifications.guide.action.redrive',
  ],
  sections: [
    { selector: '[data-guide="tabs"]', titleKey: 'notifications.guide.section.tabs.title', bodyKey: 'notifications.guide.section.tabs.body' },
    { selector: '.rules__toolbar', titleKey: 'notifications.guide.section.toolbar.title', bodyKey: 'notifications.guide.section.toolbar.body' },
    { selector: '.rules__table', titleKey: 'notifications.guide.section.table.title', bodyKey: 'notifications.guide.section.table.body' },
  ],
  workflowKeys: ['notifications.guide.flow.event', 'notifications.guide.flow.match', 'notifications.guide.flow.resolve', 'notifications.guide.flow.dispatch', 'notifications.guide.flow.retry'],
  dependsOnKeys: ['notifications.guide.dep.events', 'notifications.guide.dep.templates', 'notifications.guide.dep.users', 'notifications.guide.dep.providers'],
  usedByKeys: ['notifications.guide.use.recipients', 'notifications.guide.use.audits', 'notifications.guide.use.schedules', 'notifications.guide.use.auditTrail'],
  businessRuleKeys: ['notifications.guide.rule.systemDefault', 'notifications.guide.rule.channels', 'notifications.guide.rule.recipients', 'notifications.guide.rule.deadLetter'],
  tipKeys: ['notifications.guide.tip.preview', 'notifications.guide.tip.filter', 'notifications.guide.tip.redrive'],
  permissionKeys: ['notifications.guide.perm.configure', 'notifications.guide.perm.ops', 'notifications.guide.perm.view'],
  faq: [
    { questionKey: 'notifications.guide.faq.trigger.q', answerKey: 'notifications.guide.faq.trigger.a' },
    { questionKey: 'notifications.guide.faq.sms.q', answerKey: 'notifications.guide.faq.sms.a' },
  ],
};

@Component({
  selector: 'app-notifications',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatTabsModule,
    TranslatePipe,
    PageHeaderComponent,
    PageGuideComponent,
    NotificationRulesComponent,
    NotificationTemplatesComponent,
    NotificationDispatchesComponent,
    NotificationDeadLetterComponent,
  ],
  template: `
    <app-page-header
      [title]="'notifications.page.title' | t"
      [subtitle]="'notifications.page.subtitle' | t"
    />
    <app-page-guide [guide]="guide" />
    <mat-tab-group class="ax-tabs" data-guide="tabs">
      <mat-tab [label]="'notifications.tabs.rules' | t">
        <div class="tab-body">
          <app-notification-rules />
        </div>
      </mat-tab>
      <mat-tab [label]="'notifications.tabs.templates' | t">
        <div class="tab-body">
          <app-notification-templates />
        </div>
      </mat-tab>
      <mat-tab [label]="'notifications.tabs.dispatchLog' | t">
        <div class="tab-body">
          <app-notification-dispatches />
        </div>
      </mat-tab>
      <mat-tab [label]="'notifications.tabs.deadLetter' | t">
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
export class NotificationsComponent {
  readonly guide = NOTIFICATIONS_GUIDE;
}
