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
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';

type ViewState = 'loading' | 'ready' | 'error';

interface PreferenceFlags {
  sms_non_critical: boolean;
  digests: boolean;
}

@Component({
  selector: 'app-notification-preferences',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatCardModule,
    MatSlideToggleModule,
    MatButtonModule,
    PageHeaderComponent,
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
