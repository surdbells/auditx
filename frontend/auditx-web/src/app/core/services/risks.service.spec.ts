import { TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';

import { RisksService } from './risks.service';
import { provideTestEnv } from '../../../testing/test-providers';

const BASE = '/api/v1';

describe('RisksService', () => {
  let service: RisksService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideTestEnv()] });
    service = TestBed.inject(RisksService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists risks with filters', () => {
    service.list({ status: 'open', band: 'high', includeClosed: false, search: 'cyber', page: 1, pageSize: 25 }).subscribe();
    const req = http.expectOne((r) => r.url === `${BASE}/risks`);
    expect(req.request.params.get('status')).toBe('open');
    expect(req.request.params.get('band')).toBe('high');
    expect(req.request.params.get('includeClosed')).toBe('false');
    expect(req.request.params.get('search')).toBe('cyber');
    expect(req.request.params.get('pageSize')).toBe('25');
    req.flush({ data: { items: [], total: 0, page: 1, pageSize: 25, totalPages: 1, hasPrevious: false, hasNext: false } });
  });

  it('registers a risk', () => {
    service.register({ title: 'X', category: 'Ops', ownerUserId: 'u-1', inherentLikelihood: 4, inherentImpact: 4 }).subscribe();
    const req = http.expectOne(`${BASE}/risks`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.inherentLikelihood).toBe(4);
    req.flush({ data: {} });
  });

  it('updates a risk', () => {
    service.update('r-1', {
      title: 'X', category: 'Ops', ownerUserId: 'u-1', inherentLikelihood: 4, inherentImpact: 4,
      residualLikelihood: 2, residualImpact: 2, treatmentStrategy: 'mitigate', version: 'v1',
    }).subscribe();
    const req = http.expectOne(`${BASE}/risks/r-1`);
    expect(req.request.method).toBe('PATCH');
    req.flush({ data: {} });
  });

  it('transitions a risk', () => {
    service.transition('r-1', { status: 'closed', rationale: 'retired', version: 'v1' }).subscribe();
    const req = http.expectOne(`${BASE}/risks/r-1/transition`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ status: 'closed', rationale: 'retired', version: 'v1' });
    req.flush({ data: {} });
  });

  it('deletes a risk, URL-encoding the version', () => {
    service.delete('r-1', 'AA+/B==').subscribe();
    const req = http.expectOne(`${BASE}/risks/r-1?version=AA%2B%2FB%3D%3D`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null);
  });
});
