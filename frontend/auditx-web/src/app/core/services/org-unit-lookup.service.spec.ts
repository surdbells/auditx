import { TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';

import { OrgUnitLookupService } from './org-unit-lookup.service';
import { provideTestEnv } from '../../../testing/test-providers';
import { OrgUnit } from '../models';

const BASE = '/api/v1';

function unit(id: string, name: string, parentOrgUnitId: string | null): OrgUnit {
  return { id, name, code: id.toUpperCase(), parentOrgUnitId, headUserId: null, isArchived: false };
}

describe('OrgUnitLookupService', () => {
  let service: OrgUnitLookupService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideTestEnv()] });
    service = TestBed.inject(OrgUnitLookupService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function loadTree(): void {
    service.ensureLoaded();
    http.expectOne((r) => r.url === `${BASE}/org-units`).flush({
      data: [
        unit('a', 'Group', null),
        unit('b', 'Retail', 'a'),
        unit('c', 'Branch Ops', 'b'),
      ],
    });
  }

  it('builds full-path labels ordered by path', () => {
    loadTree();
    const labels = service.options().map((o) => o.label);
    expect(labels).toEqual([
      'Group',
      'Group / Retail',
      'Group / Retail / Branch Ops',
    ]);
  });

  it('resolves an id to its full path, and unknown/empty ids gracefully', () => {
    loadTree();
    expect(service.name('c')).toBe('Group / Retail / Branch Ops');
    expect(service.name(null)).toBe('—');
    expect(service.name('missing')).toBe('missing');
  });

  it('is cycle-guarded against a malformed parent chain', () => {
    service.ensureLoaded();
    http.expectOne((r) => r.url === `${BASE}/org-units`).flush({
      data: [unit('x', 'X', 'y'), unit('y', 'Y', 'x')], // x <-> y cycle
    });
    // Must terminate and produce a finite label rather than looping forever.
    expect(service.name('x').length).toBeGreaterThan(0);
  });

  it('loads the directory only once (idempotent ensureLoaded)', () => {
    service.ensureLoaded();
    http.expectOne((r) => r.url === `${BASE}/org-units`).flush({ data: [] });
    service.ensureLoaded();
    // No second GET is issued; verify() (afterEach) enforces that too.
    expect(http.match((r) => r.url === `${BASE}/org-units`).length).toBe(0);
  });
});
