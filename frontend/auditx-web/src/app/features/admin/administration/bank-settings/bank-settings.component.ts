import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';

import { AdministrationService } from '../../../../core/services/administration.service';
import { NotificationService } from '../../../../core/services/notification.service';
import { AuthService } from '../../../../core/services/auth.service';
import { Permissions } from '../../../../core/permissions';
import { BankSettings } from '../../../../core/models';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';

type ViewState = 'loading' | 'ready' | 'error';

@Component({
  selector: 'app-bank-settings',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatSlideToggleModule,
    TranslatePipe,
    LoadingComponent,
    ErrorStateComponent,
  ],
  templateUrl: './bank-settings.component.html',
  styleUrl: './bank-settings.component.scss',
})
export class BankSettingsComponent {
  private readonly admin = inject(AdministrationService);
  private readonly notify = inject(NotificationService);
  private readonly auth = inject(AuthService);
  private readonly fb = inject(FormBuilder);
  private readonly i18n = inject(TranslationService);

  readonly state = signal<ViewState>('loading');
  readonly saving = signal(false);
  readonly savingLimits = signal(false);

  readonly canManageSettings = computed(() =>
    this.auth.hasPermission(Permissions.ManageBankSettings),
  );
  readonly canConfigureLimits = computed(() =>
    this.auth.hasPermission(Permissions.ConfigureLimits),
  );

  readonly settingsForm = this.fb.nonNullable.group({
    bankDisplayName: ['', [Validators.required, Validators.maxLength(200)]],
    timezone: ['', Validators.required],
    localeDefault: ['', Validators.required],
    adProvisioningFilterOuDn: [''],
    adProvisioningFilterGroupSid: [''],
    allowOverlappingPlanPeriods: [false],
  });

  readonly limitsForm = this.fb.nonNullable.group({
    maxEvidenceFileMb: [0, [Validators.required, Validators.min(1)]],
    maxAuditEvidenceGb: [0, [Validators.required, Validators.min(1)]],
  });

  constructor() {
    this.fetch();
  }

  fetch(): void {
    this.state.set('loading');
    this.admin.getBankSettings().subscribe({
      next: (s) => {
        this.patch(s);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  private patch(s: BankSettings): void {
    this.settingsForm.patchValue({
      bankDisplayName: s.bankDisplayName,
      timezone: s.timezone,
      localeDefault: s.localeDefault,
      adProvisioningFilterOuDn: s.adProvisioningFilterOuDn ?? '',
      adProvisioningFilterGroupSid: s.adProvisioningFilterGroupSid ?? '',
      allowOverlappingPlanPeriods: s.allowOverlappingPlanPeriods,
    });
    this.limitsForm.patchValue({
      maxEvidenceFileMb: s.maxEvidenceFileMb,
      maxAuditEvidenceGb: s.maxAuditEvidenceGb,
    });
    if (!this.canManageSettings()) {
      this.settingsForm.disable();
    }
    if (!this.canConfigureLimits()) {
      this.limitsForm.disable();
    }
  }

  saveSettings(): void {
    if (this.settingsForm.invalid) {
      this.settingsForm.markAllAsTouched();
      return;
    }
    const v = this.settingsForm.getRawValue();
    this.saving.set(true);
    this.admin
      .updateBankSettings({
        bankDisplayName: v.bankDisplayName.trim(),
        timezone: v.timezone.trim(),
        localeDefault: v.localeDefault.trim(),
        adProvisioningFilterOuDn: v.adProvisioningFilterOuDn.trim() || null,
        adProvisioningFilterGroupSid:
          v.adProvisioningFilterGroupSid.trim() || null,
        allowOverlappingPlanPeriods: v.allowOverlappingPlanPeriods,
      })
      .subscribe({
        next: () => {
          this.notify.success(
            this.i18n.translate('administration.bankSettings.savedToast'),
          );
          this.saving.set(false);
        },
        error: () => this.saving.set(false),
      });
  }

  saveLimits(): void {
    if (this.limitsForm.invalid) {
      this.limitsForm.markAllAsTouched();
      return;
    }
    const v = this.limitsForm.getRawValue();
    this.savingLimits.set(true);
    this.admin
      .updateResourceLimits({
        maxEvidenceFileMb: v.maxEvidenceFileMb,
        maxAuditEvidenceGb: v.maxAuditEvidenceGb,
      })
      .subscribe({
        next: () => {
          this.notify.success(
            this.i18n.translate('administration.bankSettings.limitsSavedToast'),
          );
          this.savingLimits.set(false);
        },
        error: () => this.savingLimits.set(false),
      });
  }
}
