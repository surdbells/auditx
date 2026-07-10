import { TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';

import { ControlsService } from './controls.service';
import { provideTestEnv } from '../../../testing/test-providers';

const BASE = '/api/v1';

describe('ControlsService', () => {
  let service: ControlsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideTestEnv()] });
    service = TestBed.inject(ControlsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists controls with filters', () => {
    service.list({ type: 'preventive', effectiveness: 'ineffective', includeRetired: false, search: 'wire', page: 1, pageSize: 25 }).subscribe();
    const req = http.expectOne((r) => r.url === `${BASE}/controls`);
    expect(req.request.params.get('type')).toBe('preventive');
    expect(req.request.params.get('effectiveness')).toBe('ineffective');
    expect(req.request.params.get('includeRetired')).toBe('false');
    expect(req.request.params.get('search')).toBe('wire');
    expect(req.request.params.get('pageSize')).toBe('25');
    req.flush({ data: { items: [], total: 0, page: 1, pageSize: 25, totalPages: 1, hasPrevious: false, hasNext: false } });
  });

  it('registers a control', () => {
    service
      .register({ code: 'CTL-1', title: 'Dual auth', controlType: 'preventive', frequency: 'continuous', ownerUserId: 'u-1' })
      .subscribe();
    const req = http.expectOne(`${BASE}/controls`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.code).toBe('CTL-1');
    req.flush({ data: {} });
  });

  it('updates a control via PATCH', () => {
    service
      .update('c-1', {
        title: 'Dual auth',
        controlType: 'preventive',
        frequency: 'continuous',
        ownerUserId: 'u-1',
        effectiveness: 'effective',
        lastTestedDate: '2026-06-30',
        version: 'v1',
      })
      .subscribe();
    const req = http.expectOne(`${BASE}/controls/c-1`);
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body.effectiveness).toBe('effective');
    req.flush({ data: {} });
  });

  it('sets a control status via POST', () => {
    service.setStatus('c-1', { isActive: false, version: 'v1' }).subscribe();
    const req = http.expectOne(`${BASE}/controls/c-1/status`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.isActive).toBe(false);
    req.flush({ data: {} });
  });

  it('deletes a control with the version on the query string', () => {
    service.delete('c-1', 'v1').subscribe();
    const req = http.expectOne(`${BASE}/controls/c-1?version=v1`);
    expect(req.request.method).toBe('DELETE');
    req.flush({ data: null });
  });
});
