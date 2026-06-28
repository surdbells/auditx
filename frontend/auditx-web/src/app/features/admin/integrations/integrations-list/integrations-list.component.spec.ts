import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';

import { IntegrationsListComponent } from './integrations-list.component';
import { provideTestEnv } from '../../../../../testing/test-providers';
import { AuthService } from '../../../../core/services/auth.service';
import { Integration, SessionDto } from '../../../../core/models';

const BASE = '/api/v1';

function integration(overrides: Partial<Integration> = {}): Integration {
  return {
    id: 'i-1',
    type: 'smtp',
    name: 'Primary SMTP',
    connectionDetailsJson: '{}',
    hasCredentials: true,
    timeoutSeconds: 30,
    fallbackIntegrationId: null,
    isPrimary: true,
    isActive: true,
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

describe('IntegrationsListComponent', () => {
  let fixture: ComponentFixture<IntegrationsListComponent>;
  let component: IntegrationsListComponent;
  let http: HttpTestingController;

  function setup(
    permissions: string[] = [
      'ViewIntegrations',
      'ViewIntegrationHealth',
      'ConfigureIntegrations',
    ],
  ): void {
    TestBed.configureTestingModule({
      imports: [IntegrationsListComponent],
      providers: [provideTestEnv()],
    });
    const auth = TestBed.inject(AuthService);
    auth.setSession(session(permissions));

    fixture = TestBed.createComponent(IntegrationsListComponent);
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  }

  afterEach(() => http.verify());

  it('loads integrations and fetches health per row', async () => {
    setup();
    http
      .expectOne(`${BASE}/integrations`)
      .flush({ data: [integration(), integration({ id: 'i-2', name: 'SIEM', type: 'siem' })] });
    await fixture.whenStable();
    fixture.detectChanges();

    // Health requests fire for each integration.
    http
      .expectOne(`${BASE}/integrations/i-1/health`)
      .flush({
        data: {
          integrationId: 'i-1',
          state: 'healthy',
          lastSuccessAt: null,
          lastFailureAt: null,
          recentFailureCount: 0,
        },
      });
    http
      .expectOne(`${BASE}/integrations/i-2/health`)
      .flush({
        data: {
          integrationId: 'i-2',
          state: 'failing',
          lastSuccessAt: null,
          lastFailureAt: null,
          recentFailureCount: 4,
        },
      });
    await fixture.whenStable();
    fixture.detectChanges();

    expect(component.integrations().length).toBe(2);
    expect(component.healthState('i-1')).toBe('healthy');
    expect(component.healthState('i-2')).toBe('failing');
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Primary SMTP');
    // health state is capitalised for display.
    expect(text).toContain('Healthy');
  });

  it('shows the empty state when there are no integrations', async () => {
    setup();
    http.expectOne(`${BASE}/integrations`).flush({ data: [] });
    await fixture.whenStable();
    fixture.detectChanges();

    expect(component.isEmpty()).toBe(true);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('No integrations configured');
  });

  it('runs a connectivity test and surfaces the result', async () => {
    setup();
    http.expectOne(`${BASE}/integrations`).flush({ data: [integration()] });
    http.expectOne(`${BASE}/integrations/i-1/health`).flush({
      data: {
        integrationId: 'i-1',
        state: 'healthy',
        lastSuccessAt: null,
        lastFailureAt: null,
        recentFailureCount: 0,
      },
    });
    await fixture.whenStable();

    component.test(integration());
    const req = http.expectOne(`${BASE}/integrations/i-1/test`);
    expect(req.request.method).toBe('POST');
    req.flush({ data: { success: true, detail: 'OK' } });
  });

  it('hides the New integration button without ConfigureIntegrations', async () => {
    setup(['ViewIntegrations']);
    http.expectOne(`${BASE}/integrations`).flush({ data: [] });
    await fixture.whenStable();
    fixture.detectChanges();

    expect(component.canManage()).toBe(false);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).not.toContain('New integration');
  });
});
