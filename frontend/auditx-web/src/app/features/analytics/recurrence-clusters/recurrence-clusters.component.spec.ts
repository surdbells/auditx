import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';

import { RecurrenceClustersComponent } from './recurrence-clusters.component';
import { provideTestEnv } from '../../../../testing/test-providers';
import { RecurrenceCluster } from '../../../core/models';

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

describe('RecurrenceClustersComponent', () => {
  let fixture: ComponentFixture<RecurrenceClustersComponent>;
  let component: RecurrenceClustersComponent;
  let http: HttpTestingController;

  async function setup(
    items: RecurrenceCluster[],
    nextCursor: string | null,
    hasMore: boolean,
  ): Promise<void> {
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
      .flush({ data: { items, nextCursor, hasMore } });
    await fixture.whenStable();
    fixture.detectChanges();
  }

  afterEach(() => http.verify());

  it('lists the first page of clusters', async () => {
    await setup([cluster()], null, false);
    expect(component.clusters().length).toBe(1);
    expect(component.hasMore()).toBe(false);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Aml');
  });

  it('appends the next page on loadMore using the cursor', async () => {
    await setup([cluster({ id: 'c-1' })], 'cur-2', true);
    expect(component.hasMore()).toBe(true);

    component.loadMore();
    const req = http.expectOne(
      (r) => r.url === `${BASE}/analytics/recurrence-clusters`,
    );
    expect(req.request.params.get('cursor')).toBe('cur-2');
    req.flush({
      data: { items: [cluster({ id: 'c-2' })], nextCursor: null, hasMore: false },
    });

    expect(component.clusters().map((c) => c.id)).toEqual(['c-1', 'c-2']);
    expect(component.hasMore()).toBe(false);
  });

  it('shows the empty state when there are no clusters', async () => {
    await setup([], null, false);
    expect(component.isEmpty()).toBe(true);
  });
});
