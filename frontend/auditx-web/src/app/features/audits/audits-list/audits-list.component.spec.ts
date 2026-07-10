import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';

import { AuditsListComponent } from './audits-list.component';
import { provideTestEnv } from '../../../../testing/test-providers';
import { AuthService } from '../../../core/services/auth.service';
import { AuditListItem, PagedResult, SessionDto } from '../../../core/models';

const BASE = '/api/v1';

function item(id: string, name: string): AuditListItem {
  return {
    id,
    name,
    auditType: 'AML',
    status: 'draft',
    startDate: '2026-01-01',
    targetEndDate: '2026-03-01',
    leadUserId: 'u-lead',
    checklistItemCount: 4,
    respondedItemCount: 1,
  };
}

function page(
  items: AuditListItem[],
  total = items.length,
  pageNum = 1,
  pageSize = 25,
): PagedResult<AuditListItem> {
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

function session(permissions: string[]): SessionDto {
  return {
    userId: 'u1',
    email: 'a@b.c',
    firstName: 'A',
    lastName: 'B',
    displayName: 'A B',
    status: 'active',
    roles: [],
    permissions,
    expiresAt: '',
    absoluteExpiresAt: '',
  };
}

describe('AuditsListComponent', () => {
  let fixture: ComponentFixture<AuditsListComponent>;
  let component: AuditsListComponent;
  let http: HttpTestingController;

  function setup(permissions: string[] = ['ViewAudits', 'CreateAudit']): void {
    TestBed.configureTestingModule({
      imports: [AuditsListComponent],
      providers: [provideTestEnv(), provideRouter([])],
    });
    const auth = TestBed.inject(AuthService);
    auth.setSession(session(permissions));

    fixture = TestBed.createComponent(AuditsListComponent);
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  }

  /** Flushes the two requests the constructor fires: counts then first page. */
  function flushInitial(items: AuditListItem[], total = items.length): void {
    http
      .expectOne(`${BASE}/audits/counts`)
      .flush({ data: { byStatus: { draft: 2, planned: 1 } } });
    http
      .expectOne((r) => r.url === `${BASE}/audits`)
      .flush({ data: page(items, total) });
  }

  /**
   * The audit-type filter/column reads the reference-data lookup, which lazily
   * GETs the active `audit_type` items. Drain it so it doesn't leak between the
   * assertions (never-throw: flushing an empty list is enough).
   */
  function flushAuditTypeLookup(): void {
    for (const req of http.match(
      (r) => r.url === `${BASE}/reference-data/audit_type`,
    )) {
      req.flush({ data: [] });
    }
  }

  afterEach(() => {
    flushAuditTypeLookup();
    http.verify();
  });

  it('loads counts and renders audits in the table', async () => {
    setup();
    flushInitial([item('1', 'AML Review'), item('2', 'IT Audit')]);
    await fixture.whenStable();
    fixture.detectChanges();

    expect(component.audits().length).toBe(2);
    expect(component.total()).toBe(2);
    expect(component.state()).toBe('ready');
    expect(component.counts().length).toBe(2);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('AML Review');
    expect(text).toContain('IT Audit');
  });

  it('shows the empty state when no audits are returned', async () => {
    setup();
    flushInitial([]);
    await fixture.whenStable();
    fixture.detectChanges();

    expect(component.isEmpty()).toBe(true);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('No audits found');
  });

  it('filters by status, re-querying at page 1 with the chosen status', async () => {
    setup();
    flushInitial([item('1', 'AML Review')]);
    await fixture.whenStable();
    fixture.detectChanges();

    component.filters.controls.status.setValue('in_progress');
    // debounceTime(300) gates the re-query.
    await new Promise((r) => setTimeout(r, 350));

    const req = http.expectOne((r) => r.url === `${BASE}/audits`);
    expect(req.request.params.get('status')).toBe('in_progress');
    expect(req.request.params.get('page')).toBe('1');
    req.flush({ data: page([]) });
    expect(component.audits().length).toBe(0);
  });

  it('navigates to another page via the paginator, carrying page + pageSize', async () => {
    setup();
    flushInitial([item('1', 'AML Review')], 50); // total 50 → more pages
    await fixture.whenStable();
    fixture.detectChanges();

    component.onPageChange(2);
    const req = http.expectOne((r) => r.url === `${BASE}/audits`);
    expect(req.request.params.get('page')).toBe('2');
    expect(req.request.params.get('pageSize')).toBe('25');
    req.flush({ data: page([item('2', 'IT Audit')], 50, 2) });

    expect(component.audits().map((a) => a.id)).toEqual(['2']);
    expect(component.page()).toBe(2);
  });

  it('changing the page size re-queries from page 1', async () => {
    setup();
    flushInitial([item('1', 'AML Review')], 50);
    await fixture.whenStable();
    fixture.detectChanges();

    component.onPageSizeChange(100);
    const req = http.expectOne((r) => r.url === `${BASE}/audits`);
    expect(req.request.params.get('page')).toBe('1');
    expect(req.request.params.get('pageSize')).toBe('100');
    req.flush({ data: page([item('1', 'AML Review')], 50, 1, 100) });
    expect(component.pageSize()).toBe(100);
  });

  it('enters the error state when the first page fails', async () => {
    setup();
    http
      .expectOne(`${BASE}/audits/counts`)
      .flush({ data: { byStatus: {} } });
    http
      .expectOne((r) => r.url === `${BASE}/audits`)
      .flush({ title: 'Boom' }, { status: 500, statusText: 'Server Error' });
    await fixture.whenStable();
    fixture.detectChanges();
    expect(component.state()).toBe('error');
  });

  it('hides the New audit button without CreateAudit', async () => {
    setup(['ViewAudits']);
    flushInitial([]);
    await fixture.whenStable();
    fixture.detectChanges();

    expect(component.canCreate()).toBe(false);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).not.toContain('New audit');
  });
});
