import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';

import { BankSettingsComponent } from './bank-settings.component';
import { provideTestEnv } from '../../../../../testing/test-providers';
import { AuthService } from '../../../../core/services/auth.service';
import { BankSettings, SessionDto } from '../../../../core/models';

const BASE = '/api/v1';

function settings(overrides: Partial<BankSettings> = {}): BankSettings {
  return {
    bankDisplayName: 'ACME Bank',
    timezone: 'UTC',
    localeDefault: 'en-GB',
    adProvisioningFilterOuDn: null,
    adProvisioningFilterGroupSid: null,
    maxEvidenceFileMb: 25,
    maxAuditEvidenceGb: 10,
    allowOverlappingPlanPeriods: false,
    allowAuditLaunchBeforeApproval: false,
    allowMinorPlanRevisionAfterApproval: false,
    primaryColor: '#4f46e5',
    accentColor: '#7c3aed',
    logoDataUri: null,
    iconDataUri: null,
    showOverview: true,
    showWalkthrough: true,
    autoStartWalkthrough: true,
    reportRetentionMonths: 0,
    idleTimeoutMinutes: 15,
    idleWarningSeconds: 60,
    ...overrides,
  };
}

function session(permissions: string[]): SessionDto {
  return {
    userId: 'u1',
    email: 'a@b.c',
    firstName: 'A',
    lastName: 'B',
    displayName: 'A B',
    status: 'active',
    roles: [],
    permissions,
    expiresAt: '',
    absoluteExpiresAt: '',
  };
}

describe('BankSettingsComponent', () => {
  let fixture: ComponentFixture<BankSettingsComponent>;
  let component: BankSettingsComponent;
  let http: HttpTestingController;

  function setup(
    permissions: string[] = [
      'ViewBankSettings',
      'ManageBankSettings',
      'ConfigureLimits',
    ],
  ): void {
    TestBed.configureTestingModule({
      imports: [BankSettingsComponent],
      providers: [provideTestEnv()],
    });
    const auth = TestBed.inject(AuthService);
    auth.setSession(session(permissions));

    fixture = TestBed.createComponent(BankSettingsComponent);
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  }

  afterEach(() => http.verify());

  it('loads settings and populates the section forms', async () => {
    setup();
    http.expectOne(`${BASE}/admin/bank-settings`).flush({ data: settings() });
    await fixture.whenStable();
    fixture.detectChanges();

    expect(component.state()).toBe('ready');
    expect(component.orgForm.controls.bankDisplayName.value).toBe('ACME Bank');
    expect(component.limitsForm.controls.maxEvidenceFileMb.value).toBe(25);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Resource limits');
    expect(text).toContain('Organization identity');
    expect(text).toContain('Session security');
  });

  it('saves the organization section as a full merge and preserves the AD filters it does not surface', async () => {
    setup();
    http
      .expectOne(`${BASE}/admin/bank-settings`)
      .flush({
        data: settings({
          adProvisioningFilterOuDn: 'OU=Audit,DC=corp',
          adProvisioningFilterGroupSid: 'S-1-5-21-99',
        }),
      });
    await fixture.whenStable();

    component.orgForm.controls.bankDisplayName.setValue('Renamed Bank');
    component.saveOrg();
    const req = http.expectOne(`${BASE}/admin/bank-settings`);
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body.bankDisplayName).toBe('Renamed Bank');
    // The AD provisioning filters have no UI control but must ride along untouched (login enforces them).
    expect(req.request.body.adProvisioningFilterOuDn).toBe('OU=Audit,DC=corp');
    expect(req.request.body.adProvisioningFilterGroupSid).toBe('S-1-5-21-99');
    req.flush({ data: settings({ bankDisplayName: 'Renamed Bank' }) });
    expect(component.isSaving('org')).toBe(false);
  });

  it('saves only the branding section and does not drag along another section\'s unsaved edits', async () => {
    setup();
    http.expectOne(`${BASE}/admin/bank-settings`).flush({ data: settings() });
    await fixture.whenStable();

    // Unsaved edit in a DIFFERENT section — must not be persisted by a branding save.
    component.orgForm.controls.bankDisplayName.setValue('Unsaved Name');
    component.brandingForm.controls.primaryColor.setValue('#112233');
    component.saveBranding();
    const req = http.expectOne(`${BASE}/admin/bank-settings`);
    expect(req.request.body.primaryColor).toBe('#112233');
    expect(req.request.body.bankDisplayName).toBe('ACME Bank'); // baseline, not the dirty org edit
    req.flush({ data: settings({ primaryColor: '#112233' }) });
    expect(component.isSaving('branding')).toBe(false);
  });

  it('loads branding into the colour controls and logo preview', async () => {
    setup();
    http
      .expectOne(`${BASE}/admin/bank-settings`)
      .flush({
        data: settings({
          primaryColor: '#abcdef',
          logoDataUri: 'data:image/png;base64,AAA',
        }),
      });
    await fixture.whenStable();

    expect(component.brandingForm.controls.primaryColor.value).toBe('#abcdef');
    expect(component.logoPreview()).toBe('data:image/png;base64,AAA');
  });

  it('clears the logo preview and control', async () => {
    setup();
    http
      .expectOne(`${BASE}/admin/bank-settings`)
      .flush({ data: settings({ logoDataUri: 'data:image/png;base64,AAA' }) });
    await fixture.whenStable();

    component.clearLogo();
    expect(component.logoPreview()).toBeNull();
    expect(component.brandingForm.controls.logoDataUri.value).toBe('');
  });

  it('disables the section forms without write permissions', async () => {
    setup(['ViewBankSettings']);
    http.expectOne(`${BASE}/admin/bank-settings`).flush({ data: settings() });
    await fixture.whenStable();
    fixture.detectChanges();

    expect(component.canManageSettings()).toBe(false);
    expect(component.orgForm.disabled).toBe(true);
    expect(component.brandingForm.disabled).toBe(true);
    expect(component.sessionForm.disabled).toBe(true);
    expect(component.limitsForm.disabled).toBe(true);
  });
});
