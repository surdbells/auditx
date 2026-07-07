import { TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';

import { ReferenceDataLookupService } from './reference-data-lookup.service';
import { provideTestEnv } from '../../../testing/test-providers';
import { ReferenceDataItem } from '../models';

const BASE = '/api/v1';

function item(
  code: string,
  label: string,
  overrides: Partial<ReferenceDataItem> = {},
): ReferenceDataItem {
  return {
    id: `r-${code}`,
    category: 'audit_type',
    code,
    label,
    sortOrder: 0,
    isActive: true,
    ...overrides,
  };
}

describe('ReferenceDataLookupService', () => {
  let service: ReferenceDataLookupService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideTestEnv()] });
    service = TestBed.inject(ReferenceDataLookupService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lazily loads a category once and exposes ordered options', () => {
    // First read triggers the load; a second read must NOT issue a new request.
    const opts = service.options('audit_type');
    service.options('audit_type');

    const req = http.expectOne(
      (r) => r.url === `${BASE}/reference-data/audit_type`,
    );
    expect(req.request.params.get('includeInactive')).toBe('false');
    req.flush({
      data: [
        item('it', 'IT', { sortOrder: 2 }),
        item('aml', 'AML', { sortOrder: 1 }),
      ],
    });

    expect(opts().map((o) => o.code)).toEqual(['aml', 'it']);
  });

  it('resolves a code to its label, falling back to the raw code', () => {
    service.label('audit_type', 'aml');
    http
      .expectOne((r) => r.url === `${BASE}/reference-data/audit_type`)
      .flush({ data: [item('aml', 'AML')] });

    expect(service.label('audit_type', 'aml')).toBe('AML');
    // Unknown code falls back to the raw value; empty renders as an em dash.
    expect(service.label('audit_type', 'unknown')).toBe('unknown');
    expect(service.label('audit_type', '')).toBe('—');
  });

  it('never throws when the load fails; options stay empty', () => {
    const opts = service.options('exception_category');
    http
      .expectOne((r) => r.url === `${BASE}/reference-data/exception_category`)
      .flush({ title: 'Boom' }, { status: 500, statusText: 'Server Error' });

    expect(opts()).toEqual([]);
    expect(service.label('exception_category', 'fraud')).toBe('fraud');
  });
});
