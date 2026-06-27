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
});
