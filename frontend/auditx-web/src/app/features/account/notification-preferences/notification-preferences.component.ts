import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';

import { NotificationAdminService } from '../../../core/services/notifications-admin.service';
import { NotificationService } from '../../../core/services/notification.service';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { PageGuideComponent } from '../../../shared/components/page-guide/page-guide.component';
import { PageGuide } from '../../../core/models/page-guide.models';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';

type ViewState = 'loading' | 'ready' | 'error';

interface PreferenceFlags {
  sms_non_critical: boolean;
  digests: boolean;
}

/** Contextual page guide for personal notification preferences (drives the walkthrough + the About panel). */
const NOTIFICATION_PREFERENCES_GUIDE: PageGuide = {
  id: 'account-notification-preferences',
  titleKey: 'account.prefs.title',
  purposeKey: 'account.prefs.guide.purpose',
  descriptionKey: 'account.prefs.guide.description',
  actionKeys: ['account.prefs.guide.action.sms', 'account.prefs.guide.action.digests'],
  sections: [
    { selector: '.prefs__card', titleKey: 'account.prefs.guide.section.toggles.title', bodyKey: 'account.prefs.guide.section.toggles.body' },
  ],
  businessRuleKeys: ['account.prefs.guide.rule.criticalAlways'],
  permissionKeys: ['account.prefs.guide.perm.anyUser'],
};

@Component({
  selector: 'app-notification-preferences',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatCardModule,
    MatSlideToggleModule,
    MatButtonModule,
    PageHeaderComponent,
    PageGuideComponent,
    LoadingComponent,
    ErrorStateComponent,
    TranslatePipe,
  ],
  templateUrl: './notification-preferences.component.html',
  styleUrl: './notification-preferences.component.scss',
})
export class NotificationPreferencesComponent {
  private readonly notifications = inject(NotificationAdminService);
  private readonly notify = inject(NotificationService);
  private readonly i18n = inject(TranslationService);

  readonly state = signal<ViewState>('loading');
  readonly saving = signal(false);

  readonly smsNonCritical = signal(false);
  readonly digests = signal(false);

  readonly guide = NOTIFICATION_PREFERENCES_GUIDE;

  constructor() {
    this.fetch();
  }

  fetch(): void {
    this.state.set('loading');
    this.notifications.getMyPreferences().subscribe({
      next: (prefs) => {
        const flags = this.parse(prefs.preferencesJson);
        this.smsNonCritical.set(flags.sms_non_critical);
        this.digests.set(flags.digests);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  private parse(json: string | null): PreferenceFlags {
    if (!json) {
      return { sms_non_critical: false, digests: false };
    }
    try {
      const parsed = JSON.parse(json) as Partial<PreferenceFlags>;
      return {
        sms_non_critical: parsed.sms_non_critical === true,
        digests: parsed.digests === true,
      };
    } catch {
      return { sms_non_critical: false, digests: false };
    }
  }

  save(): void {
    this.saving.set(true);
    const preferencesJson = JSON.stringify({
      sms_non_critical: this.smsNonCritical(),
      digests: this.digests(),
    });
    this.notifications.updateMyPreferences({ preferencesJson }).subscribe({
      next: () => {
        this.saving.set(false);
        this.notify.success(this.i18n.translate('account.prefs.saved'));
      },
      error: () => this.saving.set(false),
    });
  }
}
