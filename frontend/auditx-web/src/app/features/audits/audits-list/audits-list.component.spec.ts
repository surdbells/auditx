import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';

import { AuditsListComponent } from './audits-list.component';
import { provideTestEnv } from '../../../../testing/test-providers';
import { AuthService } from '../../../core/services/auth.service';
import { AuditListItem, CursorPage, SessionDto } from '../../../core/models';

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
  hasMore = false,
): CursorPage<AuditListItem> {
  return { items, nextCursor: hasMore ? 'cursor-2' : null, hasMore };
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
  function flushInitial(items: AuditListItem[], hasMore = false): void {
    http
      .expectOne(`${BASE}/audits/counts`)
      .flush({ data: { byStatus: { draft: 2, planned: 1 } } });
    http
      .expectOne((r) => r.url === `${BASE}/audits`)
      .flush({ data: page(items, hasMore) });
  }

  afterEach(() => http.verify());

  it('loads counts and renders audits in the table', async () => {
    setup();
    flushInitial([item('1', 'AML Review'), item('2', 'IT Audit')]);
    await fixture.whenStable();
    fixture.detectChanges();

    expect(component.audits().length).toBe(2);
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

  it('filters by status, re-querying with the chosen status', async () => {
    setup();
    flushInitial([item('1', 'AML Review')]);
    await fixture.whenStable();
    fixture.detectChanges();

    component.filters.controls.status.setValue('in_progress');
    // debounceTime(300) gates the re-query.
    await new Promise((r) => setTimeout(r, 350));

    const req = http.expectOne((r) => r.url === `${BASE}/audits`);
    expect(req.request.params.get('status')).toBe('in_progress');
    req.flush({ data: page([]) });
    expect(component.audits().length).toBe(0);
  });

  it('appends results when loadMore is invoked', async () => {
    setup();
    flushInitial([item('1', 'AML Review')], true);
    await fixture.whenStable();
    fixture.detectChanges();

    expect(component.hasMore()).toBe(true);
    component.loadMore();
    const req = http.expectOne((r) => r.url === `${BASE}/audits`);
    expect(req.request.params.get('cursor')).toBe('cursor-2');
    req.flush({ data: page([item('2', 'IT Audit')], false) });

    expect(component.audits().length).toBe(2);
    expect(component.hasMore()).toBe(false);
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
