import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';

import { ExceptionsListComponent } from './exceptions-list.component';
import { provideTestEnv } from '../../../../testing/test-providers';
import { ExceptionListItem } from '../../../core/models';

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

describe('ExceptionsListComponent', () => {
  let fixture: ComponentFixture<ExceptionsListComponent>;
  let component: ExceptionsListComponent;
  let http: HttpTestingController;

  async function setup(): Promise<void> {
    TestBed.configureTestingModule({
      imports: [ExceptionsListComponent],
      providers: [provideTestEnv(), provideRouter([])],
    });

    fixture = TestBed.createComponent(ExceptionsListComponent);
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    await fixture.whenStable();

    // ensureUsers loads the active users.
    http.expectOne((r) => r.url === `${BASE}/users`).flush({
      data: {
        items: [{ id: 'u-owner', displayName: 'Olive Owner' }],
        nextCursor: null,
        hasMore: false,
      },
    });
    // The plan + audit directories back the dropdown filters (+ audit-name column).
    http.expectOne((r) => r.url === `${BASE}/annual-plans`).flush({
      data: {
        items: [{ id: 'p-1', periodLabel: 'FY2026' }],
        nextCursor: null,
        hasMore: false,
      },
    });
    http.expectOne((r) => r.url === `${BASE}/audits`).flush({
      data: {
        items: [{ id: 'a-1', name: 'Treasury Controls Audit' }],
        nextCursor: null,
        hasMore: false,
      },
    });
    // The first page of exceptions.
    http
      .expectOne((r) => r.url === `${BASE}/exceptions`)
      .flush({
        data: { items: [item()], nextCursor: 'c2', hasMore: true },
      });
    fixture.detectChanges();
    await fixture.whenStable();
  }

  afterEach(() => http.verify());

  it('loads the first page and resolves owner names', async () => {
    await setup();
    expect(component.state()).toBe('ready');
    expect(component.exceptions().length).toBe(1);
    expect(component.hasMore()).toBe(true);
    expect(component.nameOf('u-owner')).toBe('Olive Owner');
  });

  it('appends a second page on load more', async () => {
    await setup();
    component.loadMore();

    const req = http.expectOne((r) => r.url === `${BASE}/exceptions`);
    expect(req.request.params.get('cursor')).toBe('c2');
    req.flush({
      data: { items: [item({ id: 'x-2' })], nextCursor: null, hasMore: false },
    });

    expect(component.exceptions().length).toBe(2);
    expect(component.hasMore()).toBe(false);
  });

  it('sends overdue and recurrence filters to the query', async () => {
    await setup();
    component.filters.patchValue({ overdue: 'yes', recurrence: 'no' });
    // Re-query directly (the form's valueChanges is debounced).
    component.fetchFirstPage();

    const req = http.expectOne((r) => r.url === `${BASE}/exceptions`);
    expect(req.request.params.get('overdue')).toBe('true');
    expect(req.request.params.get('recurrence')).toBe('false');
    req.flush({ data: { items: [], nextCursor: null, hasMore: false } });
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
    component.fetchFirstPage();

    const req = http.expectOne((r) => r.url === `${BASE}/exceptions`);
    expect(req.request.params.get('search')).toBe('wire transfer');
    expect(req.request.params.get('plan')).toBe('p-1');
    expect(req.request.params.get('audit')).toBe('a-1');
    // Dates are serialized to ISO datetimes (exact value is timezone-dependent).
    expect(req.request.params.get('raisedFrom')).toMatch(/^\d{4}-\d{2}-\d{2}T/);
    expect(req.request.params.get('raisedTo')).toMatch(/^\d{4}-\d{2}-\d{2}T/);
    req.flush({ data: { items: [], nextCursor: null, hasMore: false } });
    expect(component.exceptions().length).toBe(0);
  });

  it('resolves the audit id to its name for the grid', async () => {
    await setup();
    expect(component.auditNameOf('a-1')).toBe('Treasury Controls Audit');
    expect(component.auditNameOf('unknown')).toBe('unknown');
  });
});
