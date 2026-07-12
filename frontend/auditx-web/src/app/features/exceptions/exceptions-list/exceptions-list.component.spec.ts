import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';

import { ExceptionsListComponent } from './exceptions-list.component';
import { provideTestEnv } from '../../../../testing/test-providers';
import { ExceptionListItem, PagedResult } from '../../../core/models';

const BASE = '/api/v1';

function item(overrides: Partial<ExceptionListItem> = {}): ExceptionListItem {
  return {
    id: 'x-1',
    auditId: 'a-1',
    title: 'Missing control',
    severity: 'high',
    status: 'open',
    ownerUserId: 'u-owner',
    targetDate: '2026-02-01',
    isOverdue: true,
    daysPastTarget: 5,
    isRecurrence: false,
    raisedAt: '2026-01-15T09:00:00Z',
    ...overrides,
  };
}

function page(
  items: ExceptionListItem[],
  total = items.length,
  pageNum = 1,
  pageSize = 25,
): PagedResult<ExceptionListItem> {
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

describe('ExceptionsListComponent', () => {
  let fixture: ComponentFixture<ExceptionsListComponent>;
  let component: ExceptionsListComponent;
  let http: HttpTestingController;

  async function setup(total = 50): Promise<void> {
    TestBed.configureTestingModule({
      imports: [ExceptionsListComponent],
      providers: [provideTestEnv(), provideRouter([])],
    });

    fixture = TestBed.createComponent(ExceptionsListComponent);
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    await fixture.whenStable();

    // Owner names resolve via the directory-backed lookup (all users, no admin permission).
    http.expectOne((r) => r.url === `${BASE}/users/directory`).flush({
      data: {
        items: [{ id: 'u-owner', displayName: 'Olive Owner' }],
        total: 1,
        page: 1,
        pageSize: 5000,
        totalPages: 1,
        hasPrevious: false,
        hasNext: false,
      },
    });
    // The plan directory backs the plan dropdown filter (still cursor-paged).
    http.expectOne((r) => r.url === `${BASE}/annual-plans`).flush({
      data: {
        items: [{ id: 'p-1', periodLabel: 'FY2026' }],
        nextCursor: null,
        hasMore: false,
      },
    });
    // The audit directory backs the audit dropdown filter (+ audit-name column) — one capped "load all".
    http.expectOne((r) => r.url === `${BASE}/audits`).flush({
      data: {
        items: [{ id: 'a-1', name: 'Treasury Controls Audit' }],
        total: 1,
        page: 1,
        pageSize: 5000,
        totalPages: 1,
        hasPrevious: false,
        hasNext: false,
      },
    });
    // The first page of exceptions.
    http
      .expectOne((r) => r.url === `${BASE}/exceptions`)
      .flush({ data: page([item()], total) });
    // The saved-views bar loads this screen's views on init.
    http.expectOne((r) => r.url === `${BASE}/saved-views`).flush({ data: [] });
    fixture.detectChanges();
    await fixture.whenStable();
  }

  afterEach(() => http.verify());

  it('loads the first page and resolves owner names', async () => {
    await setup();
    expect(component.state()).toBe('ready');
    expect(component.exceptions().length).toBe(1);
    expect(component.total()).toBe(50);
    expect(component.nameOf('u-owner')).toBe('Olive Owner');
  });

  it('navigates to another page via the paginator, carrying page + pageSize', async () => {
    await setup();
    component.onPageChange(2);

    const req = http.expectOne((r) => r.url === `${BASE}/exceptions`);
    expect(req.request.params.get('page')).toBe('2');
    expect(req.request.params.get('pageSize')).toBe('25');
    req.flush({ data: page([item({ id: 'x-2' })], 50, 2) });

    expect(component.exceptions().map((e) => e.id)).toEqual(['x-2']);
    expect(component.page()).toBe(2);
  });

  it('changing the page size re-queries from page 1', async () => {
    await setup();
    component.onPageSizeChange(100);

    const req = http.expectOne((r) => r.url === `${BASE}/exceptions`);
    expect(req.request.params.get('page')).toBe('1');
    expect(req.request.params.get('pageSize')).toBe('100');
    req.flush({ data: page([item()], 50, 1, 100) });

    expect(component.pageSize()).toBe(100);
  });

  it('sends overdue and recurrence filters to the query', async () => {
    await setup();
    component.filters.patchValue({ overdue: 'yes', recurrence: 'no' });
    // Re-query directly (the form's valueChanges is debounced).
    component.fetchPage(1);

    const req = http.expectOne((r) => r.url === `${BASE}/exceptions`);
    expect(req.request.params.get('overdue')).toBe('true');
    expect(req.request.params.get('recurrence')).toBe('false');
    req.flush({ data: page([]) });
    expect(component.exceptions().length).toBe(0);
  });

  it('sends search, plan, audit and raised-date filters to the query', async () => {
    await setup();
    component.filters.patchValue({
      search: '  wire transfer ',
      plan: 'p-1',
      audit: 'a-1',
      raisedFrom: new Date(2026, 0, 1),
      raisedTo: new Date(2026, 2, 31),
    });
    component.fetchPage(1);

    const req = http.expectOne((r) => r.url === `${BASE}/exceptions`);
    expect(req.request.params.get('search')).toBe('wire transfer');
    expect(req.request.params.get('plan')).toBe('p-1');
    expect(req.request.params.get('audit')).toBe('a-1');
    // Dates are serialized to ISO datetimes (exact value is timezone-dependent).
    expect(req.request.params.get('raisedFrom')).toMatch(/^\d{4}-\d{2}-\d{2}T/);
    expect(req.request.params.get('raisedTo')).toMatch(/^\d{4}-\d{2}-\d{2}T/);
    req.flush({ data: page([]) });
    expect(component.exceptions().length).toBe(0);
  });

  it('resolves the audit id to its name for the grid', async () => {
    await setup();
    expect(component.auditNameOf('a-1')).toBe('Treasury Controls Audit');
    expect(component.auditNameOf('unknown')).toBe('unknown');
  });

  it('round-trips filters through a saved view (currentParams → applyView)', async () => {
    await setup();
    component.filters.patchValue({
      search: 'sod',
      severity: 'critical',
      overdue: 'yes',
      raisedFrom: new Date(2026, 0, 1),
    });

    // Capture the current selection as a saved view would (dates serialised to ISO strings).
    const saved = component.currentParams();
    expect(saved['search']).toBe('sod');
    expect(saved['severity']).toBe('critical');
    expect(typeof saved['raisedFrom']).toBe('string');

    // Reset, then re-apply the saved parameters.
    component.filters.reset({
      search: '', status: 'all', severity: 'all', plan: '', audit: '',
      overdue: 'all', recurrence: 'all', raisedFrom: null, raisedTo: null,
    });
    component.applyView(saved);

    const restored = component.filters.getRawValue();
    expect(restored.search).toBe('sod');
    expect(restored.severity).toBe('critical');
    expect(restored.overdue).toBe('yes');
    expect(restored.raisedFrom instanceof Date).toBe(true);

    // patchValue's debounced watcher issues a refetch — satisfy it so http.verify() passes.
    component.fetchPage(1);
    http.expectOne((r) => r.url === `${BASE}/exceptions`).flush({ data: page([]) });
  });
});
