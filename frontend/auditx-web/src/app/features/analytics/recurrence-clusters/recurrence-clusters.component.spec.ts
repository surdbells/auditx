import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';

import { RecurrenceClustersComponent } from './recurrence-clusters.component';
import { provideTestEnv } from '../../../../testing/test-providers';
import { PagedResult, RecurrenceCluster } from '../../../core/models';

const BASE = '/api/v1';

function cluster(overrides: Partial<RecurrenceCluster> = {}): RecurrenceCluster {
  return {
    id: 'c-1',
    auditableEntityId: 'e-1',
    category: 'aml',
    closedExceptionCount: 4,
    windowMonths: 12,
    firstOccurredAt: '2026-01-01T00:00:00Z',
    lastOccurredAt: '2026-05-01T00:00:00Z',
    detectedAt: '2026-06-01T00:00:00Z',
    notifiedAt: null,
    ...overrides,
  };
}

function page(
  items: RecurrenceCluster[],
  total = items.length,
  pageNum = 1,
  pageSize = 25,
): PagedResult<RecurrenceCluster> {
  const totalPages = Math.max(1, Math.ceil(total / pageSize));
  return {
    items,
    total,
    page: pageNum,
    pageSize,
    totalPages,
    hasPrevious: pageNum > 1,
    hasNext: pageNum < totalPages,
  };
}

describe('RecurrenceClustersComponent', () => {
  let fixture: ComponentFixture<RecurrenceClustersComponent>;
  let component: RecurrenceClustersComponent;
  let http: HttpTestingController;

  async function setup(result: PagedResult<RecurrenceCluster>): Promise<void> {
    TestBed.configureTestingModule({
      imports: [RecurrenceClustersComponent],
      providers: [provideTestEnv(), provideRouter([])],
    });
    fixture = TestBed.createComponent(RecurrenceClustersComponent);
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();

    http
      .expectOne((r) => r.url === `${BASE}/analytics/recurrence-clusters`)
      .flush({ data: result });
    await fixture.whenStable();
    fixture.detectChanges();

    // Rendering an entity name lazily loads the entity directory.
    flushEntityDirectory();
  }

  /** Flushes the (at most one) lazy entity-directory GET the entity label triggers. */
  function flushEntityDirectory(): void {
    for (const req of http.match((r) => r.url === `${BASE}/audit-universe/entities`)) {
      req.flush({ data: { items: [], total: 0, page: 1, pageSize: 5000, totalPages: 1, hasPrevious: false, hasNext: false } });
    }
  }

  afterEach(() => {
    flushEntityDirectory();
    http.verify();
  });

  it('lists the first page of clusters', async () => {
    await setup(page([cluster()]));
    expect(component.clusters().length).toBe(1);
    expect(component.total()).toBe(1);
    expect(component.page()).toBe(1);
    expect(component.state()).toBe('ready');
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Aml');
  });

  it('navigates to another page via the paginator, carrying page + pageSize', async () => {
    await setup(page([cluster({ id: 'c-1' })], 50));

    component.onPageChange(2);
    const req = http.expectOne(
      (r) => r.url === `${BASE}/analytics/recurrence-clusters`,
    );
    expect(req.request.params.get('page')).toBe('2');
    expect(req.request.params.get('pageSize')).toBe('25');
    req.flush({ data: page([cluster({ id: 'c-2' })], 50, 2) });

    expect(component.clusters().map((c) => c.id)).toEqual(['c-2']);
    expect(component.page()).toBe(2);
  });

  it('changing the page size re-queries from page 1', async () => {
    await setup(page([cluster({ id: 'c-1' })], 50));

    component.onPageSizeChange(100);
    const req = http.expectOne(
      (r) => r.url === `${BASE}/analytics/recurrence-clusters`,
    );
    expect(req.request.params.get('page')).toBe('1');
    expect(req.request.params.get('pageSize')).toBe('100');
    req.flush({ data: page([cluster({ id: 'c-1' })], 50, 1, 100) });
    expect(component.pageSize()).toBe(100);
  });

  it('shows the empty state when there are no clusters', async () => {
    await setup(page([]));
    expect(component.isEmpty()).toBe(true);
  });
});
