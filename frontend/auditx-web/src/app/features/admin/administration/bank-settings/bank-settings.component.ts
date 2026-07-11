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
import { BrandingService } from '../../../../core/services/branding.service';
import { Permissions } from '../../../../core/permissions';
import { BankSettings } from '../../../../core/models';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { PageGuideComponent } from '../../../../shared/components/page-guide/page-guide.component';
import { PageGuide } from '../../../../core/models/page-guide.models';

type ViewState = 'loading' | 'ready' | 'error';

/** Contextual page guide for the organization/bank settings page. */
const BANK_SETTINGS_GUIDE: PageGuide = {
  id: 'administration-bank-settings',
  titleKey: 'administration.bankSettings.title',
  purposeKey: 'administration.bankSettings.guide.purpose',
  descriptionKey: 'administration.bankSettings.guide.description',
  actionKeys: [
    'administration.bankSettings.guide.action.identity',
    'administration.bankSettings.guide.action.branding',
    'administration.bankSettings.guide.action.rules',
    'administration.bankSettings.guide.action.limits',
  ],
  sections: [
    { selector: '[data-guide="general"]', titleKey: 'administration.bankSettings.guide.section.general.title', bodyKey: 'administration.bankSettings.guide.section.general.body' },
    { selector: '.settings__branding', titleKey: 'administration.bankSettings.guide.section.branding.title', bodyKey: 'administration.bankSettings.guide.section.branding.body' },
    { selector: '[data-guide="limits"]', titleKey: 'administration.bankSettings.guide.section.limits.title', bodyKey: 'administration.bankSettings.guide.section.limits.body' },
  ],
  workflowKeys: [
    'administration.bankSettings.guide.flow.identity',
    'administration.bankSettings.guide.flow.brand',
    'administration.bankSettings.guide.flow.rules',
    'administration.bankSettings.guide.flow.save',
  ],
  dependsOnKeys: [
    'administration.bankSettings.guide.dep.permissions',
    'administration.bankSettings.guide.dep.directory',
  ],
  usedByKeys: [
    'administration.bankSettings.guide.use.branding',
    'administration.bankSettings.guide.use.planning',
    'administration.bankSettings.guide.use.evidence',
  ],
  businessRuleKeys: [
    'administration.bankSettings.guide.rule.color',
    'administration.bankSettings.guide.rule.asset',
    'administration.bankSettings.guide.rule.overlap',
    'administration.bankSettings.guide.rule.limits',
  ],
  tipKeys: [
    'administration.bankSettings.guide.tip.preview',
    'administration.bankSettings.guide.tip.locale',
  ],
  permissionKeys: [
    'administration.bankSettings.guide.perm.manage',
    'administration.bankSettings.guide.perm.limits',
  ],
  faq: [
    { questionKey: 'administration.bankSettings.guide.faq.apply.q', answerKey: 'administration.bankSettings.guide.faq.apply.a' },
    { questionKey: 'administration.bankSettings.guide.faq.limits.q', answerKey: 'administration.bankSettings.guide.faq.limits.a' },
  ],
};

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
    PageGuideComponent,
  ],
  templateUrl: './bank-settings.component.html',
  styleUrl: './bank-settings.component.scss',
})
export class BankSettingsComponent {
  private readonly admin = inject(AdministrationService);
  private readonly notify = inject(NotificationService);
  private readonly auth = inject(AuthService);
  private readonly branding = inject(BrandingService);
  private readonly fb = inject(FormBuilder);
  private readonly i18n = inject(TranslationService);

  readonly guide = BANK_SETTINGS_GUIDE;

  /** Max branding image size, in bytes (mirrors the server-side cap). */
  private readonly maxAssetBytes = 512 * 1024;

  readonly state = signal<ViewState>('loading');
  readonly saving = signal(false);
  readonly savingLimits = signal(false);
  /** Data-URI previews for the logo/icon (mirror the form controls so OnPush re-renders on async reads). */
  readonly logoPreview = signal<string | null>(null);
  readonly iconPreview = signal<string | null>(null);

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
    allowAuditLaunchBeforeApproval: [false],
    showOverview: [true],
    showWalkthrough: [true],
    autoStartWalkthrough: [true],
    reportRetentionMonths: [0, [Validators.required, Validators.min(0), Validators.max(600)]],
    idleTimeoutMinutes: [15, [Validators.required, Validators.min(0), Validators.max(480)]],
    // 20-second floor per WCAG 2.2.1 (Timing Adjustable): users must get at least 20s to extend the session.
    idleWarningSeconds: [60, [Validators.required, Validators.min(20), Validators.max(600)]],
    primaryColor: [
      '#4f46e5',
      [Validators.required, Validators.pattern(/^#[0-9a-fA-F]{6}$/)],
    ],
    accentColor: [
      '#7c3aed',
      [Validators.required, Validators.pattern(/^#[0-9a-fA-F]{6}$/)],
    ],
    logoDataUri: [''],
    iconDataUri: [''],
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
      allowAuditLaunchBeforeApproval: s.allowAuditLaunchBeforeApproval,
      showOverview: s.showOverview,
      showWalkthrough: s.showWalkthrough,
      autoStartWalkthrough: s.autoStartWalkthrough,
      reportRetentionMonths: s.reportRetentionMonths,
      idleTimeoutMinutes: s.idleTimeoutMinutes,
      idleWarningSeconds: s.idleWarningSeconds,
      primaryColor: s.primaryColor,
      accentColor: s.accentColor,
      logoDataUri: s.logoDataUri ?? '',
      iconDataUri: s.iconDataUri ?? '',
    });
    this.logoPreview.set(s.logoDataUri ?? null);
    this.iconPreview.set(s.iconDataUri ?? null);
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
    const logoDataUri = v.logoDataUri || null;
    const iconDataUri = v.iconDataUri || null;
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
        allowAuditLaunchBeforeApproval: v.allowAuditLaunchBeforeApproval,
        showOverview: v.showOverview,
        showWalkthrough: v.showWalkthrough,
        autoStartWalkthrough: v.autoStartWalkthrough,
        reportRetentionMonths: v.reportRetentionMonths,
        idleTimeoutMinutes: v.idleTimeoutMinutes,
        idleWarningSeconds: v.idleWarningSeconds,
        primaryColor: v.primaryColor,
        accentColor: v.accentColor,
        logoDataUri,
        iconDataUri,
      })
      .subscribe({
        next: () => {
          // Re-theme the running app in place (incl. the page-guide button visibility) so the change is visible without a reload.
          this.branding.apply({
            organizationName: v.bankDisplayName.trim(),
            primaryColor: v.primaryColor,
            accentColor: v.accentColor,
            logoDataUri,
            iconDataUri,
            showOverview: v.showOverview,
            showWalkthrough: v.showWalkthrough,
            autoStartWalkthrough: v.autoStartWalkthrough,
            idleTimeoutMinutes: v.idleTimeoutMinutes,
            idleWarningSeconds: v.idleWarningSeconds,
          });
          this.notify.success(
            this.i18n.translate('administration.bankSettings.savedToast'),
          );
          this.saving.set(false);
        },
        error: () => this.saving.set(false),
      });
  }

  /** Reads a chosen logo file into the form as a data URI (with client-side type/size guards). */
  onLogoSelected(event: Event): void {
    this.readImageInto(event, 'logoDataUri');
  }

  /** Reads a chosen icon file into the form as a data URI (with client-side type/size guards). */
  onIconSelected(event: Event): void {
    this.readImageInto(event, 'iconDataUri');
  }

  clearLogo(): void {
    this.settingsForm.controls.logoDataUri.setValue('');
    this.settingsForm.controls.logoDataUri.markAsDirty();
    this.logoPreview.set(null);
  }

  clearIcon(): void {
    this.settingsForm.controls.iconDataUri.setValue('');
    this.settingsForm.controls.iconDataUri.markAsDirty();
    this.iconPreview.set(null);
  }

  private readImageInto(
    event: Event,
    control: 'logoDataUri' | 'iconDataUri',
  ): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) {
      return;
    }
    if (!file.type.startsWith('image/') || file.size > this.maxAssetBytes) {
      this.notify.error(
        this.i18n.translate('administration.branding.invalidImage'),
      );
      input.value = '';
      return;
    }
    const preview = control === 'logoDataUri' ? this.logoPreview : this.iconPreview;
    const reader = new FileReader();
    reader.onload = () => {
      const dataUri = reader.result as string;
      this.settingsForm.controls[control].setValue(dataUri);
      this.settingsForm.controls[control].markAsDirty();
      preview.set(dataUri);
    };
    reader.readAsDataURL(file);
    // Allow re-selecting the same file later.
    input.value = '';
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
