import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';

import { TemplatesListComponent } from './templates-list.component';
import { provideTestEnv } from '../../../../../testing/test-providers';
import { AuthService } from '../../../../core/services/auth.service';
import { PagedResult, SessionDto, TemplateListItem } from '../../../../core/models';

const BASE = '/api/v1';

function item(id: string, name: string): TemplateListItem {
  return {
    id,
    name,
    auditType: 'AML',
    description: '',
    status: 'published',
    currentVersion: 1,
    itemCount: 3,
  };
}

function page(
  items: TemplateListItem[],
  total = items.length,
  pageNum = 1,
  pageSize = 25,
): PagedResult<TemplateListItem> {
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

describe('TemplatesListComponent', () => {
  let fixture: ComponentFixture<TemplatesListComponent>;
  let component: TemplatesListComponent;
  let http: HttpTestingController;

  function setup(permissions: string[] = ['ViewTemplates', 'ManageTemplates']): void {
    TestBed.configureTestingModule({
      imports: [TemplatesListComponent],
      providers: [provideTestEnv(), provideRouter([])],
    });
    const auth = TestBed.inject(AuthService);
    auth.setSession(session(permissions));

    fixture = TestBed.createComponent(TemplatesListComponent);
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
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

  it('loads and renders templates in the table', async () => {
    setup();
    http
      .expectOne((r) => r.url === `${BASE}/templates`)
      .flush({ data: page([item('1', 'AML Review'), item('2', 'IT Audit')]) });
    await fixture.whenStable();
    fixture.detectChanges();

    expect(component.templates().length).toBe(2);
    expect(component.state()).toBe('ready');
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('AML Review');
    expect(text).toContain('IT Audit');
  });

  it('shows the empty state when no templates are returned', async () => {
    setup();
    http.expectOne((r) => r.url === `${BASE}/templates`).flush({ data: page([]) });
    await fixture.whenStable();
    fixture.detectChanges();

    expect(component.isEmpty()).toBe(true);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('No templates found');
  });

  it('navigates to another page via the paginator, carrying page + pageSize', async () => {
    setup();
    http
      .expectOne((r) => r.url === `${BASE}/templates`)
      .flush({ data: page([item('1', 'AML Review')], 50) }); // total 50 → more pages
    await fixture.whenStable();
    fixture.detectChanges();

    component.onPageChange(2);
    const req = http.expectOne((r) => r.url === `${BASE}/templates`);
    expect(req.request.params.get('page')).toBe('2');
    expect(req.request.params.get('pageSize')).toBe('25');
    req.flush({ data: page([item('2', 'IT Audit')], 50, 2) });

    expect(component.templates().map((t) => t.id)).toEqual(['2']);
    expect(component.page()).toBe(2);
  });

  it('changing the page size re-queries from page 1', async () => {
    setup();
    http
      .expectOne((r) => r.url === `${BASE}/templates`)
      .flush({ data: page([item('1', 'AML Review')], 50) });
    await fixture.whenStable();
    fixture.detectChanges();

    component.onPageSizeChange(100);
    const req = http.expectOne((r) => r.url === `${BASE}/templates`);
    expect(req.request.params.get('page')).toBe('1');
    expect(req.request.params.get('pageSize')).toBe('100');
    req.flush({ data: page([item('1', 'AML Review')], 50, 1, 100) });
    expect(component.pageSize()).toBe(100);
  });

  it('enters the error state when the first page fails', async () => {
    setup();
    http
      .expectOne((r) => r.url === `${BASE}/templates`)
      .flush({ title: 'Boom' }, { status: 500, statusText: 'Server Error' });
    await fixture.whenStable();
    fixture.detectChanges();
    expect(component.state()).toBe('error');
  });

  it('hides the New template button without ManageTemplates', async () => {
    setup(['ViewTemplates']);
    http.expectOne((r) => r.url === `${BASE}/templates`).flush({ data: page([]) });
    await fixture.whenStable();
    fixture.detectChanges();

    expect(component.canManage()).toBe(false);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).not.toContain('New template');
  });
});
