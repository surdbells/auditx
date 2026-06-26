import { TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';

import { IntegrationsService } from './integrations.service';
import { provideTestEnv } from '../../../testing/test-providers';
import { Integration } from '../models';

const BASE = '/api/v1';

function integration(overrides: Partial<Integration> = {}): Integration {
  return {
    id: 'i-1',
    type: 'Smtp',
    name: 'Primary SMTP',
    connectionDetailsJson: '{"host":"mail"}',
    hasCredentials: true,
    timeoutSeconds: 30,
    fallbackIntegrationId: null,
    isPrimary: true,
    isActive: true,
    ...overrides,
  };
}

describe('IntegrationsService', () => {
  let service: IntegrationsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideTestEnv()] });
    service = TestBed.inject(IntegrationsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists integrations and unwraps data', () => {
    let result: Integration[] | undefined;
    service.list().subscribe((r) => (result = r));
    const req = http.expectOne(`${BASE}/integrations`);
    expect(req.request.method).toBe('GET');
    req.flush({ data: [integration()] });
    expect(result?.length).toBe(1);
    expect(result?.[0].name).toBe('Primary SMTP');
  });

  it('fetches integration health', () => {
    let state: string | undefined;
    service.health('i-1').subscribe((h) => (state = h.state));
    http.expectOne(`${BASE}/integrations/i-1/health`).flush({
      data: {
        integrationId: 'i-1',
        state: 'Degraded',
        lastSuccessAt: null,
        lastFailureAt: null,
        recentFailureCount: 2,
      },
    });
    expect(state).toBe('Degraded');
  });

  it('creates an integration with the supplied body', () => {
    service
      .create({
        type: 'Webhook',
        name: 'Hook',
        connectionDetailsJson: '{}',
        credentials: 'secret',
        timeoutSeconds: 10,
        fallbackIntegrationId: null,
      })
      .subscribe();
    const req = http.expectOne(`${BASE}/integrations`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.type).toBe('Webhook');
    expect(req.request.body.credentials).toBe('secret');
    req.flush({ data: integration({ type: 'Webhook' }) });
  });

  it('patches an integration', () => {
    service
      .update('i-1', {
        name: 'Renamed',
        connectionDetailsJson: '{}',
        credentials: null,
        timeoutSeconds: 20,
        fallbackIntegrationId: null,
        isActive: true,
      })
      .subscribe();
    const req = http.expectOne(`${BASE}/integrations/i-1`);
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body.name).toBe('Renamed');
    req.flush({ data: integration({ name: 'Renamed' }) });
  });

  it('deactivates an integration via DELETE 204', () => {
    let done = false;
    service.deactivate('i-1').subscribe(() => (done = true));
    const req = http.expectOne(`${BASE}/integrations/i-1`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null, { status: 204, statusText: 'No Content' });
    expect(done).toBe(true);
  });

  it('tests an integration and returns success/detail', () => {
    let result: { success: boolean; detail: string } | undefined;
    service.test('i-1').subscribe((r) => (result = r));
    const req = http.expectOne(`${BASE}/integrations/i-1/test`);
    expect(req.request.method).toBe('POST');
    req.flush({ data: { success: true, detail: 'Connected' } });
    expect(result?.success).toBe(true);
    expect(result?.detail).toBe('Connected');
  });
});
