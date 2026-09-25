import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';

import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';
import { IconComponent } from '../../../core/icons/icon.component';
import { UsersService } from '../../../core/services/users.service';
import { NotificationService } from '../../../core/services/notification.service';
import { TimezoneService, zoneOffset } from '../../../core/services/timezone.service';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import {
  SearchableSelectComponent,
  SelectOption,
} from '../../../shared/components/searchable-select/searchable-select.component';

/** All IANA zones the platform knows, labelled with their current offset (e.g. "Africa/Lagos (GMT+0100)"). */
function timezoneOptions(): SelectOption[] {
  const intl = Intl as unknown as { supportedValuesOf?: (key: string) => string[] };
  let zones: string[];
  try {
    zones = intl.supportedValuesOf
      ? intl.supportedValuesOf('timeZone')
      : [Intl.DateTimeFormat().resolvedOptions().timeZone];
  } catch {
    zones = ['UTC'];
  }
  return zones.map((z) => ({ value: z, label: `${z} (GMT${offsetLabel(z)})` }));
}

function offsetLabel(zone: string): string {
  const o = zoneOffset(zone);
  return o === 'UTC' ? '+0000' : o;
}

const LOCALES: SelectOption[] = [
  { value: 'en-GB', label: 'English (UK) — en-GB' },
  { value: 'en-US', label: 'English (US) — en-US' },
  { value: 'fr-FR', label: 'Français — fr-FR' },
];

/** Personal display preferences: the timezone (and locale) every timestamp in the app is shown in. */
@Component({
  selector: 'app-preferences',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatSelectModule,
    MatButtonModule,
    IconComponent,
    TranslatePipe,
    PageHeaderComponent,
    SearchableSelectComponent,
  ],
  template: `
    <app-page-header
      [title]="'account.preferences.title' | t"
      [subtitle]="'account.preferences.subtitle' | t"
    />

    <mat-card appearance="outlined" class="prefs">
      <mat-card-content>
        <form [formGroup]="form" class="prefs__form">
          <p class="prefs__detected">
            {{ 'account.preferences.detected' | t: { zone: detected } }}
            <button matButton type="button" (click)="useDetected()">
              <app-icon name="schedule" />
              {{ 'account.preferences.useDetected' | t }}
            </button>
          </p>

          <app-searchable-select
            class="prefs__field"
            formControlName="timezone"
            [label]="'account.preferences.timezone' | t"
            [options]="zones"
          />

          <mat-form-field appearance="outline" class="prefs__field">
            <mat-label>{{ 'account.preferences.locale' | t }}</mat-label>
            <mat-select formControlName="locale">
              @for (l of locales; track l.value) {
                <mat-option [value]="l.value">{{ l.label }}</mat-option>
              }
            </mat-select>
          </mat-form-field>

          <div class="prefs__actions">
            <button matButton="filled" type="button" (click)="save()" [disabled]="form.invalid">
              {{ 'account.preferences.save' | t }}
            </button>
          </div>
        </form>
      </mat-card-content>
    </mat-card>
  `,
  styles: `
    .prefs {
      max-width: 640px;
    }
    .prefs__form {
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
    }
    .prefs__field {
      width: 100%;
    }
    .prefs__detected {
      display: flex;
      align-items: center;
      gap: 0.75rem;
      flex-wrap: wrap;
      color: var(--mat-sys-on-surface-variant);
      margin: 0 0 0.5rem;
    }
    .prefs__actions {
      display: flex;
      justify-content: flex-end;
    }
  `,
})
export class PreferencesComponent {
  private readonly users = inject(UsersService);
  private readonly tz = inject(TimezoneService);
  private readonly notify = inject(NotificationService);
  private readonly i18n = inject(TranslationService);
  private readonly fb = inject(FormBuilder);

  readonly zones = timezoneOptions();
  readonly locales = LOCALES;
  readonly detected = this.tz.browserZone;

  readonly form = this.fb.nonNullable.group({
    timezone: this.tz.zone(),
    locale: this.tz.locale(),
  });

  constructor() {
    this.users.myPreferences().subscribe((p) => {
      this.form.patchValue({
        timezone: p.timezone && p.timezone !== 'UTC' ? p.timezone : this.detected,
        locale: p.locale || this.tz.locale(),
      });
    });
  }

  useDetected(): void {
    this.form.patchValue({ timezone: this.detected });
  }

  save(): void {
    const { timezone, locale } = this.form.getRawValue();
    this.users.updateMyPreferences({ timezone, locale }).subscribe({
      next: () => {
        this.tz.setZone(timezone);
        this.tz.setLocale(locale);
        this.notify.success(this.i18n.translate('account.preferences.saved'));
      },
    });
  }
}
