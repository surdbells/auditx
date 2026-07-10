import { TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';

import { RegulationsService } from './regulations.service';
import { provideTestEnv } from '../../../testing/test-providers';

const BASE = '/api/v1';

describe('RegulationsService', () => {
  let service: RegulationsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideTestEnv()] });
    service = TestBed.inject(RegulationsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists regulations with filters', () => {
    service.list({ category: 'AML', includeRetired: false, search: 'cbn', page: 1, pageSize: 25 }).subscribe();
    const req = http.expectOne((r) => r.url === `${BASE}/regulations`);
    expect(req.request.params.get('category')).toBe('AML');
    expect(req.request.params.get('includeRetired')).toBe('false');
    expect(req.request.params.get('search')).toBe('cbn');
    expect(req.request.params.get('pageSize')).toBe('25');
    req.flush({ data: { items: [], total: 0, page: 1, pageSize: 25, totalPages: 1, hasPrevious: false, hasNext: false } });
  });

  it('registers a regulation', () => {
    service.register({ code: 'REG-1', name: 'AML/CFT', authority: 'Central Bank', category: 'AML' }).subscribe();
    const req = http.expectOne(`${BASE}/regulations`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.code).toBe('REG-1');
    req.flush({ data: {} });
  });

  it('updates a regulation via PATCH', () => {
    service.update('r-1', { name: 'AML/CFT 2022', authority: 'Central Bank', version: 'v1' }).subscribe();
    const req = http.expectOne(`${BASE}/regulations/r-1`);
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body.name).toBe('AML/CFT 2022');
    req.flush({ data: {} });
  });

  it('sets a regulation status via POST', () => {
    service.setStatus('r-1', { isActive: false, version: 'v1' }).subscribe();
    const req = http.expectOne(`${BASE}/regulations/r-1/status`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.isActive).toBe(false);
    req.flush({ data: {} });
  });

  it('deletes a regulation with the version on the query string', () => {
    service.delete('r-1', 'v1').subscribe();
    const req = http.expectOne(`${BASE}/regulations/r-1?version=v1`);
    expect(req.request.method).toBe('DELETE');
    req.flush({ data: null });
  });
});
