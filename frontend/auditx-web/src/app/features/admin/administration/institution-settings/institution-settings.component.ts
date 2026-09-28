import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
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
import { InstitutionSettings, UpdateInstitutionSettingsRequest } from '../../../../core/models';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { PageGuideComponent } from '../../../../shared/components/page-guide/page-guide.component';
import { PageGuide } from '../../../../core/models/page-guide.models';

type ViewState = 'loading' | 'ready' | 'error';

/** The independently-savable settings sections on this page. */
type SaveGroup =
  | 'org'
  | 'branding'
  | 'governance'
  | 'session'
  | 'guide'
  | 'reports'
  | 'limits';

/** Contextual page guide for the organization/bank settings page. */
const BANK_SETTINGS_GUIDE: PageGuide = {
  id: 'administration-institution-settings',
  titleKey: 'administration.institutionSettings.title',
  purposeKey: 'administration.institutionSettings.guide.purpose',
  descriptionKey: 'administration.institutionSettings.guide.description',
  actionKeys: [
    'administration.institutionSettings.guide.action.identity',
    'administration.institutionSettings.guide.action.branding',
    'administration.institutionSettings.guide.action.rules',
    'administration.institutionSettings.guide.action.limits',
  ],
  sections: [
    { selector: '[data-guide="general"]', titleKey: 'administration.institutionSettings.guide.section.general.title', bodyKey: 'administration.institutionSettings.guide.section.general.body' },
    { selector: '.settings__branding', titleKey: 'administration.institutionSettings.guide.section.branding.title', bodyKey: 'administration.institutionSettings.guide.section.branding.body' },
    { selector: '[data-guide="limits"]', titleKey: 'administration.institutionSettings.guide.section.limits.title', bodyKey: 'administration.institutionSettings.guide.section.limits.body' },
  ],
  workflowKeys: [
    'administration.institutionSettings.guide.flow.identity',
    'administration.institutionSettings.guide.flow.brand',
    'administration.institutionSettings.guide.flow.rules',
    'administration.institutionSettings.guide.flow.save',
  ],
  dependsOnKeys: [
    'administration.institutionSettings.guide.dep.permissions',
    'administration.institutionSettings.guide.dep.directory',
  ],
  usedByKeys: [
    'administration.institutionSettings.guide.use.branding',
    'administration.institutionSettings.guide.use.planning',
    'administration.institutionSettings.guide.use.evidence',
  ],
  businessRuleKeys: [
    'administration.institutionSettings.guide.rule.color',
    'administration.institutionSettings.guide.rule.asset',
    'administration.institutionSettings.guide.rule.overlap',
    'administration.institutionSettings.guide.rule.limits',
  ],
  tipKeys: [
    'administration.institutionSettings.guide.tip.preview',
    'administration.institutionSettings.guide.tip.locale',
  ],
  permissionKeys: [
    'administration.institutionSettings.guide.perm.manage',
    'administration.institutionSettings.guide.perm.limits',
  ],
  faq: [
    { questionKey: 'administration.institutionSettings.guide.faq.apply.q', answerKey: 'administration.institutionSettings.guide.faq.apply.a' },
    { questionKey: 'administration.institutionSettings.guide.faq.limits.q', answerKey: 'administration.institutionSettings.guide.faq.limits.a' },
  ],
};

@Component({
  selector: 'app-institution-settings',
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
  templateUrl: './institution-settings.component.html',
  styleUrl: './institution-settings.component.scss',
})
export class InstitutionSettingsComponent {
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
  /** Sections whose save is in flight — each card shows its own spinner and disables only its own button. */
  private readonly savingGroups = signal<ReadonlySet<SaveGroup>>(new Set());

  /** True while the given section's save is in flight. */
  isSaving(group: SaveGroup): boolean {
    return this.savingGroups().has(group);
  }
  private setSaving(group: SaveGroup, on: boolean): void {
    this.savingGroups.update((s) => {
      const next = new Set(s);
      if (on) {
        next.add(group);
      } else {
        next.delete(group);
      }
      return next;
    });
  }
  /** Data-URI previews for the logo/icon (mirror the form controls so OnPush re-renders on async reads). */
  readonly logoPreview = signal<string | null>(null);
  readonly iconPreview = signal<string | null>(null);

  /**
   * Last-persisted settings. Every institution-settings PATCH is a full replace, so each section save merges
   * ITS values onto this baseline — that keeps saves independent (one section's unsaved edits are never
   * dragged along by another section's save) and preserves fields with no UI control (the AD provisioning
   * filters, which login provisioning still enforces).
   */
  private loaded: InstitutionSettings | null = null;

  readonly canManageSettings = computed(() =>
    this.auth.hasPermission(Permissions.ManageInstitutionSettings),
  );
  readonly canConfigureLimits = computed(() =>
    this.auth.hasPermission(Permissions.ConfigureLimits),
  );

  readonly orgForm = this.fb.nonNullable.group({
    institutionDisplayName: ['', [Validators.required, Validators.maxLength(200)]],
    timezone: ['', Validators.required],
    localeDefault: ['', Validators.required],
  });

  readonly brandingForm = this.fb.nonNullable.group({
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

  readonly governanceForm = this.fb.nonNullable.group({
    allowOverlappingPlanPeriods: [false],
    allowAuditLaunchBeforeApproval: [false],
    allowMinorPlanRevisionAfterApproval: [false],
  });

  readonly sessionForm = this.fb.nonNullable.group({
    idleTimeoutMinutes: [15, [Validators.required, Validators.min(0), Validators.max(480)]],
    // 20-second floor per WCAG 2.2.1 (Timing Adjustable): users must get at least 20s to extend the session.
    idleWarningSeconds: [60, [Validators.required, Validators.min(20), Validators.max(600)]],
  });

  readonly guideForm = this.fb.nonNullable.group({
    showOverview: [true],
    showWalkthrough: [true],
    autoStartWalkthrough: [true],
  });

  readonly reportsForm = this.fb.nonNullable.group({
    reportRetentionMonths: [0, [Validators.required, Validators.min(0), Validators.max(600)]],
  });

  readonly limitsForm = this.fb.nonNullable.group({
    maxEvidenceFileMb: [0, [Validators.required, Validators.min(1)]],
    maxAuditEvidenceGb: [0, [Validators.required, Validators.min(1)]],
  });

  /** All page-guide-permission-gated forms (everything except resource limits, which has its own permission). */
  private readonly manageForms = [
    this.orgForm,
    this.brandingForm,
    this.governanceForm,
    this.sessionForm,
    this.guideForm,
    this.reportsForm,
  ];

  constructor() {
    this.fetch();
  }

  fetch(): void {
    this.state.set('loading');
    this.admin.getInstitutionSettings().subscribe({
      next: (s) => {
        this.patch(s);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  private patch(s: InstitutionSettings): void {
    this.loaded = s;
    this.orgForm.reset({
      institutionDisplayName: s.institutionDisplayName,
      timezone: s.timezone,
      localeDefault: s.localeDefault,
    });
    this.brandingForm.reset({
      primaryColor: s.primaryColor,
      accentColor: s.accentColor,
      logoDataUri: s.logoDataUri ?? '',
      iconDataUri: s.iconDataUri ?? '',
    });
    this.governanceForm.reset({
      allowOverlappingPlanPeriods: s.allowOverlappingPlanPeriods,
      allowAuditLaunchBeforeApproval: s.allowAuditLaunchBeforeApproval,
      allowMinorPlanRevisionAfterApproval: s.allowMinorPlanRevisionAfterApproval,
    });
    this.sessionForm.reset({
      idleTimeoutMinutes: s.idleTimeoutMinutes,
      idleWarningSeconds: s.idleWarningSeconds,
    });
    this.guideForm.reset({
      showOverview: s.showOverview,
      showWalkthrough: s.showWalkthrough,
      autoStartWalkthrough: s.autoStartWalkthrough,
    });
    this.reportsForm.reset({ reportRetentionMonths: s.reportRetentionMonths });
    this.limitsForm.reset({
      maxEvidenceFileMb: s.maxEvidenceFileMb,
      maxAuditEvidenceGb: s.maxAuditEvidenceGb,
    });
    this.logoPreview.set(s.logoDataUri ?? null);
    this.iconPreview.set(s.iconDataUri ?? null);
    if (!this.canManageSettings()) {
      this.manageForms.forEach((f) => f.disable());
    }
    if (!this.canConfigureLimits()) {
      this.limitsForm.disable();
    }
  }

  /** The last-persisted settings as a full update request — the baseline each section save overrides. */
  private baseRequest(): UpdateInstitutionSettingsRequest {
    const b = this.loaded!;
    return {
      institutionDisplayName: b.institutionDisplayName,
      timezone: b.timezone,
      localeDefault: b.localeDefault,
      // No UI control — preserved verbatim so a settings save never wipes the login-provisioning filters.
      adProvisioningFilterOuDn: b.adProvisioningFilterOuDn,
      adProvisioningFilterGroupSid: b.adProvisioningFilterGroupSid,
      allowOverlappingPlanPeriods: b.allowOverlappingPlanPeriods,
      allowAuditLaunchBeforeApproval: b.allowAuditLaunchBeforeApproval,
      allowMinorPlanRevisionAfterApproval: b.allowMinorPlanRevisionAfterApproval,
      primaryColor: b.primaryColor,
      accentColor: b.accentColor,
      logoDataUri: b.logoDataUri,
      iconDataUri: b.iconDataUri,
      showOverview: b.showOverview,
      showWalkthrough: b.showWalkthrough,
      autoStartWalkthrough: b.autoStartWalkthrough,
      reportRetentionMonths: b.reportRetentionMonths,
      idleTimeoutMinutes: b.idleTimeoutMinutes,
      idleWarningSeconds: b.idleWarningSeconds,
    };
  }

  /** Merges one section's values onto the baseline and PATCHes; re-themes the app from the authoritative response. */
  private saveGroup(
    group: SaveGroup,
    form: AbstractControl,
    overrides: Partial<UpdateInstitutionSettingsRequest>,
  ): void {
    if (form.invalid) {
      form.markAllAsTouched();
      return;
    }
    if (!this.loaded || this.isSaving(group)) {
      return;
    }
    this.setSaving(group, true);
    // Apply this section's values to the baseline before sending, so a concurrent save of a
    // different card merges onto them (each save is a full replace) and can't clobber this change.
    this.loaded = { ...this.loaded, ...overrides } as InstitutionSettings;
    this.admin
      .updateInstitutionSettings({ ...this.baseRequest(), ...overrides })
      .subscribe({
        next: (saved) => {
          this.loaded = saved;
          // Re-theme the running app in place (branding + page-guide visibility + idle policy) without a reload.
          this.branding.apply({
            organizationName: saved.institutionDisplayName,
            primaryColor: saved.primaryColor,
            accentColor: saved.accentColor,
            logoDataUri: saved.logoDataUri,
            iconDataUri: saved.iconDataUri,
            showOverview: saved.showOverview,
            showWalkthrough: saved.showWalkthrough,
            autoStartWalkthrough: saved.autoStartWalkthrough,
            idleTimeoutMinutes: saved.idleTimeoutMinutes,
            idleWarningSeconds: saved.idleWarningSeconds,
          });
          this.notify.success(
            this.i18n.translate('administration.institutionSettings.savedToast'),
          );
          this.setSaving(group, false);
        },
        error: () => {
          this.setSaving(group, false);
          // The optimistic baseline may be ahead of the server — refetch the source of truth.
          this.fetch();
        },
      });
  }

  saveOrg(): void {
    const v = this.orgForm.getRawValue();
    this.saveGroup('org', this.orgForm, {
      institutionDisplayName: v.institutionDisplayName.trim(),
      timezone: v.timezone.trim(),
      localeDefault: v.localeDefault.trim(),
    });
  }

  saveBranding(): void {
    const v = this.brandingForm.getRawValue();
    this.saveGroup('branding', this.brandingForm, {
      primaryColor: v.primaryColor,
      accentColor: v.accentColor,
      logoDataUri: v.logoDataUri || null,
      iconDataUri: v.iconDataUri || null,
    });
  }

  saveGovernance(): void {
    const v = this.governanceForm.getRawValue();
    this.saveGroup('governance', this.governanceForm, {
      allowOverlappingPlanPeriods: v.allowOverlappingPlanPeriods,
      allowAuditLaunchBeforeApproval: v.allowAuditLaunchBeforeApproval,
      allowMinorPlanRevisionAfterApproval: v.allowMinorPlanRevisionAfterApproval,
    });
  }

  saveSession(): void {
    const v = this.sessionForm.getRawValue();
    this.saveGroup('session', this.sessionForm, {
      idleTimeoutMinutes: v.idleTimeoutMinutes,
      idleWarningSeconds: v.idleWarningSeconds,
    });
  }

  saveGuide(): void {
    const v = this.guideForm.getRawValue();
    this.saveGroup('guide', this.guideForm, {
      showOverview: v.showOverview,
      showWalkthrough: v.showWalkthrough,
      autoStartWalkthrough: v.autoStartWalkthrough,
    });
  }

  saveReports(): void {
    const v = this.reportsForm.getRawValue();
    this.saveGroup('reports', this.reportsForm, {
      reportRetentionMonths: v.reportRetentionMonths,
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
    this.brandingForm.controls.logoDataUri.setValue('');
    this.brandingForm.controls.logoDataUri.markAsDirty();
    this.logoPreview.set(null);
  }

  clearIcon(): void {
    this.brandingForm.controls.iconDataUri.setValue('');
    this.brandingForm.controls.iconDataUri.markAsDirty();
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
      this.brandingForm.controls[control].setValue(dataUri);
      this.brandingForm.controls[control].markAsDirty();
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
    if (this.isSaving('limits')) {
      return;
    }
    const v = this.limitsForm.getRawValue();
    this.setSaving('limits', true);
    this.admin
      .updateResourceLimits({
        maxEvidenceFileMb: v.maxEvidenceFileMb,
        maxAuditEvidenceGb: v.maxAuditEvidenceGb,
      })
      .subscribe({
        next: () => {
          if (this.loaded) {
            this.loaded = {
              ...this.loaded,
              maxEvidenceFileMb: v.maxEvidenceFileMb,
              maxAuditEvidenceGb: v.maxAuditEvidenceGb,
            };
          }
          this.notify.success(
            this.i18n.translate('administration.institutionSettings.limitsSavedToast'),
          );
          this.setSaving('limits', false);
        },
        error: () => this.setSaving('limits', false),
      });
  }
}
