import { TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';

import { UniverseService } from './universe.service';
import { provideTestEnv } from '../../../testing/test-providers';
import { Entity } from '../models';

const BASE = '/api/v1';

function entity(overrides: Partial<Entity> = {}): Entity {
  return {
    id: 'e-1',
    entityType: 'Process',
    name: 'Wire Transfers',
    description: '',
    parentEntityId: null,
    ownerUserId: null,
    orgUnitId: null,
    inherentScores: {},
    residualScores: {},
    compositeInherentScore: null,
    compositeResidualScore: null,
    lastAuditedAt: null,
    expectedAuditsPerYear: null,
    version: 1,
    ...overrides,
  };
}

describe('UniverseService', () => {
  let service: UniverseService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideTestEnv()] });
    service = TestBed.inject(UniverseService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists entities with query params and unwraps the page', () => {
    let result: { items: unknown[] } | undefined;
    service
      .list({ entityType: 'Process', search: 'wire', archived: false, page: 1, pageSize: 25 })
      .subscribe((page) => (result = page));

    const req = http.expectOne((r) => r.url === `${BASE}/audit-universe/entities`);
    expect(req.request.params.get('entityType')).toBe('Process');
    expect(req.request.params.get('search')).toBe('wire');
    expect(req.request.params.get('archived')).toBe('false');
    expect(req.request.params.get('pageSize')).toBe('25');
    req.flush({ data: { items: [entity()], total: 1, page: 1, pageSize: 25, totalPages: 1, hasPrevious: false, hasNext: false } });

    expect(result?.items.length).toBe(1);
  });

  it('gets a single entity and unwraps data', () => {
    let result: Entity | undefined;
    service.getById('e-1').subscribe((e) => (result = e));
    http
      .expectOne(`${BASE}/audit-universe/entities/e-1`)
      .flush({ data: entity() });
    expect(result?.id).toBe('e-1');
  });

  it('creates an entity with a POST', () => {
    let result: Entity | undefined;
    service
      .create({ name: 'New', entityType: 'System' })
      .subscribe((e) => (result = e));
    const req = http.expectOne(`${BASE}/audit-universe/entities`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.name).toBe('New');
    req.flush({ data: entity({ id: 'e-2', name: 'New' }) });
    expect(result?.id).toBe('e-2');
  });

  it('updates an entity carrying the version for concurrency', () => {
    service
      .update('e-1', { name: 'X', entityType: 'Process', version: 3 })
      .subscribe();
    const req = http.expectOne(`${BASE}/audit-universe/entities/e-1`);
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body.version).toBe(3);
    req.flush({ data: entity({ version: 4 }) });
  });

  it('archives an entity via a void DELETE', () => {
    let done = false;
    service.archive('e-1').subscribe(() => (done = true));
    const req = http.expectOne(`${BASE}/audit-universe/entities/e-1`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null, { status: 204, statusText: 'No Content' });
    expect(done).toBe(true);
  });

  it('saves risk scores carrying the version', () => {
    let result: Entity | undefined;
    service
      .saveRiskScores('e-1', { inherentScores: { Financial: 3 }, version: 2 })
      .subscribe((e) => (result = e));
    const req = http.expectOne(
      `${BASE}/audit-universe/entities/e-1/risk-scores`,
    );
    expect(req.request.method).toBe('POST');
    expect(req.request.body.version).toBe(2);
    expect(req.request.body.inherentScores.Financial).toBe(3);
    req.flush({ data: entity({ compositeInherentScore: 3 }) });
    expect(result?.compositeInherentScore).toBe(3);
  });

  it('fetches risk-score history', () => {
    service.riskScoreHistory('e-1').subscribe();
    const req = http.expectOne(
      `${BASE}/audit-universe/entities/e-1/risk-scores/history`,
    );
    expect(req.request.method).toBe('GET');
    req.flush({ data: [] });
  });

  it('bulk-imports CSV content', () => {
    let result: { created: number } | undefined;
    service.bulkImport({ csvContent: 'a,b' }).subscribe((r) => (result = r));
    const req = http.expectOne(
      `${BASE}/audit-universe/entities/bulk-import`,
    );
    expect(req.request.body.csvContent).toBe('a,b');
    req.flush({ data: { created: 5, errors: [] } });
    expect(result?.created).toBe(5);
  });

  it('lists entity types', () => {
    let result: string[] | undefined;
    service.entityTypes().subscribe((t) => (result = t));
    http
      .expectOne(`${BASE}/audit-universe/entity-types`)
      .flush({ data: ['Process', 'System'] });
    expect(result?.length).toBe(2);
  });

  it('deletes an entity type URL-encoded', () => {
    let done = false;
    service.deleteEntityType('Business Unit').subscribe(() => (done = true));
    const req = http.expectOne(
      (r) =>
        r.method === 'DELETE' &&
        r.url === `${BASE}/audit-universe/entity-types/Business%20Unit`,
    );
    req.flush(null, { status: 204, statusText: 'No Content' });
    expect(done).toBe(true);
  });
});
