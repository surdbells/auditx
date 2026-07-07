import { TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';

import { ReferenceDataService } from './reference-data.service';
import { provideTestEnv } from '../../../testing/test-providers';
import { ReferenceDataItem } from '../models';

const BASE = '/api/v1';

function item(overrides: Partial<ReferenceDataItem> = {}): ReferenceDataItem {
  return {
    id: 'r-1',
    category: 'audit_type',
    code: 'aml',
    label: 'AML',
    description: undefined,
    sortOrder: 1,
    isActive: true,
    ...overrides,
  };
}

describe('ReferenceDataService', () => {
  let service: ReferenceDataService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideTestEnv()] });
    service = TestBed.inject(ReferenceDataService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists the categories', () => {
    let result: { code: string; label: string }[] | undefined;
    service.categories().subscribe((c) => (result = c));
    const req = http.expectOne(`${BASE}/reference-data/categories`);
    expect(req.request.method).toBe('GET');
    req.flush({
      data: [{ code: 'audit_type', label: 'Audit types' }],
    });
    expect(result?.length).toBe(1);
  });

  it('lists active items by default', () => {
    let result: ReferenceDataItem[] | undefined;
    service.list('audit_type').subscribe((i) => (result = i));
    const req = http.expectOne(
      (r) => r.url === `${BASE}/reference-data/audit_type`,
    );
    expect(req.request.params.get('includeInactive')).toBe('false');
    req.flush({ data: [item()] });
    expect(result?.length).toBe(1);
  });

  it('includes inactive items when asked', () => {
    service.list('audit_type', true).subscribe();
    const req = http.expectOne(
      (r) => r.url === `${BASE}/reference-data/audit_type`,
    );
    expect(req.request.params.get('includeInactive')).toBe('true');
    req.flush({ data: [] });
  });

  it('creates an item', () => {
    let result: ReferenceDataItem | undefined;
    service
      .create('audit_type', { code: 'it', label: 'IT', sortOrder: 2 })
      .subscribe((i) => (result = i));
    const req = http.expectOne(`${BASE}/reference-data/audit_type`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.code).toBe('it');
    req.flush({ data: item({ id: 'r-2', code: 'it', label: 'IT' }) });
    expect(result?.id).toBe('r-2');
  });

  it('updates an item with a PATCH', () => {
    service
      .update('audit_type', 'r-1', { label: 'AML Review', sortOrder: 3 })
      .subscribe();
    const req = http.expectOne(`${BASE}/reference-data/audit_type/r-1`);
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body.label).toBe('AML Review');
    req.flush({ data: item({ label: 'AML Review', sortOrder: 3 }) });
  });

  it('archives an item', () => {
    service.archive('audit_type', 'r-1').subscribe();
    const req = http.expectOne(
      `${BASE}/reference-data/audit_type/r-1/archive`,
    );
    expect(req.request.method).toBe('POST');
    req.flush(null);
  });

  it('reactivates an item', () => {
    service.reactivate('audit_type', 'r-1').subscribe();
    const req = http.expectOne(
      `${BASE}/reference-data/audit_type/r-1/reactivate`,
    );
    expect(req.request.method).toBe('POST');
    req.flush(null);
  });
});
