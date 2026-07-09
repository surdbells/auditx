import { TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';

import { OrgUnitService } from './org-unit.service';
import { provideTestEnv } from '../../../testing/test-providers';
import { OrgUnit } from '../models';

const BASE = '/api/v1';

function unit(overrides: Partial<OrgUnit> = {}): OrgUnit {
  return {
    id: 'o-1',
    name: 'HQ',
    code: 'HQ',
    parentOrgUnitId: null,
    isArchived: false,
    ...overrides,
  };
}

describe('OrgUnitService', () => {
  let service: OrgUnitService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideTestEnv()] });
    service = TestBed.inject(OrgUnitService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists active units by default and passes includeArchived', () => {
    service.list().subscribe();
    const req = http.expectOne((r) => r.url === `${BASE}/org-units`);
    expect(req.request.params.get('includeArchived')).toBe('false');
    req.flush({ data: [unit()] });

    service.list(true).subscribe();
    const req2 = http.expectOne((r) => r.url === `${BASE}/org-units`);
    expect(req2.request.params.get('includeArchived')).toBe('true');
    req2.flush({ data: [] });
  });

  it('creates a unit', () => {
    let created: OrgUnit | undefined;
    service
      .create({ name: 'Retail', code: 'RET', parentOrgUnitId: 'o-1' })
      .subscribe((u) => (created = u));
    const req = http.expectOne(`${BASE}/org-units`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ name: 'Retail', code: 'RET', parentOrgUnitId: 'o-1' });
    req.flush({ data: unit({ id: 'o-2', name: 'Retail', code: 'RET', parentOrgUnitId: 'o-1' }) });
    expect(created?.id).toBe('o-2');
  });

  it('renames via the rename endpoint', () => {
    service.rename('o-1', { name: 'Head Office' }).subscribe();
    const req = http.expectOne(`${BASE}/org-units/o-1/rename`);
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body).toEqual({ name: 'Head Office' });
    req.flush({ data: unit({ name: 'Head Office' }) });
  });

  it('reparents via the parent endpoint', () => {
    service.reparent('o-2', { parentOrgUnitId: null }).subscribe();
    const req = http.expectOne(`${BASE}/org-units/o-2/parent`);
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body).toEqual({ parentOrgUnitId: null });
    req.flush({ data: unit({ id: 'o-2', parentOrgUnitId: null }) });
  });

  it('archives and restores', () => {
    service.archive('o-1').subscribe();
    const a = http.expectOne(`${BASE}/org-units/o-1/archive`);
    expect(a.request.method).toBe('POST');
    a.flush({ data: unit({ isArchived: true }) });

    service.restore('o-1').subscribe();
    const r = http.expectOne(`${BASE}/org-units/o-1/restore`);
    expect(r.request.method).toBe('POST');
    r.flush({ data: unit({ isArchived: false }) });
  });
});
