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

  it('loads settings and populates the forms', async () => {
    setup();
    http.expectOne(`${BASE}/admin/bank-settings`).flush({ data: settings() });
    await fixture.whenStable();
    fixture.detectChanges();

    expect(component.state()).toBe('ready');
    expect(component.settingsForm.controls.bankDisplayName.value).toBe('ACME Bank');
    expect(component.limitsForm.controls.maxEvidenceFileMb.value).toBe(25);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Resource limits');
  });

  it('PATCHes bank settings on save', async () => {
    setup();
    http.expectOne(`${BASE}/admin/bank-settings`).flush({ data: settings() });
    await fixture.whenStable();

    component.settingsForm.controls.bankDisplayName.setValue('Renamed Bank');
    component.saveSettings();
    const req = http.expectOne(`${BASE}/admin/bank-settings`);
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body.bankDisplayName).toBe('Renamed Bank');
    req.flush({ data: settings({ bankDisplayName: 'Renamed Bank' }) });
    expect(component.saving()).toBe(false);
  });

  it('disables forms without write permissions', async () => {
    setup(['ViewBankSettings']);
    http.expectOne(`${BASE}/admin/bank-settings`).flush({ data: settings() });
    await fixture.whenStable();
    fixture.detectChanges();

    expect(component.canManageSettings()).toBe(false);
    expect(component.settingsForm.disabled).toBe(true);
    expect(component.limitsForm.disabled).toBe(true);
  });
});
