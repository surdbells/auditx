import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';

import { AcActionItemsComponent } from './action-items.component';
import { provideTestEnv } from '../../../../testing/test-providers';
import { AuthService } from '../../../core/services/auth.service';
import { AcActionItem, PagedResult, SessionDto } from '../../../core/models';

const BASE = '/api/v1';

function page(
  items: AcActionItem[],
  total = items.length,
  pageNum = 1,
  pageSize = 25,
): PagedResult<AcActionItem> {
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

/** Empty offset page envelope for the active-user directory lookup. */
function emptyUserPage(): unknown {
  return {
    items: [],
    total: 0,
    page: 1,
    pageSize: 5000,
    totalPages: 1,
    hasPrevious: false,
    hasNext: false,
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

function actionItem(overrides: Partial<AcActionItem> = {}): AcActionItem {
  return {
    id: 'a-1',
    title: 'Follow up on AML gap',
    description: null,
    status: 'open',
    assignedToUserId: null,
    dueDate: null,
    closureResponse: null,
    createdByUserId: 'u-1',
    closedAt: null,
    closedByUserId: null,
    acknowledgedAt: null,
    acknowledgedByUserId: null,
    version: 'av1',
    ...overrides,
  };
}

describe('AcActionItemsComponent', () => {
  let fixture: ComponentFixture<AcActionItemsComponent>;
  let component: AcActionItemsComponent;
  let http: HttpTestingController;

  async function setup(
    perms: string[],
    items: AcActionItem[],
  ): Promise<void> {
    TestBed.configureTestingModule({
      imports: [AcActionItemsComponent],
      providers: [provideTestEnv()],
    });
    const auth = TestBed.inject(AuthService);
    auth.setSession(session(perms));

    fixture = TestBed.createComponent(AcActionItemsComponent);
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();

    await fixture.whenStable();
    http
      .expectOne((r) => r.url === `${BASE}/ac-action-items`)
      .flush({ data: page(items) });
    await fixture.whenStable();
    // Component fetches the active-user directory for assignee labels.
    http
      .expectOne((r) => r.url === `${BASE}/users`)
      .flush({ data: emptyUserPage() });
    await fixture.whenStable();
    fixture.detectChanges();
  }

  afterEach(() => http.verify());

  it('lists action items and renders the status', async () => {
    await setup(['ACMember'], [actionItem()]);
    expect(component.items().length).toBe(1);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Follow up on AML gap');
    expect(text).toContain('Open');
  });

  it('shows the empty state when no items are returned', async () => {
    await setup(['ACMember'], []);
    expect(component.isEmpty()).toBe(true);
  });

  it('gates create on ACMember', async () => {
    await setup(['CIA'], [actionItem()]);
    expect(component.canCreate()).toBe(false);
    expect(component.canManage()).toBe(true);
  });

  it('gates acknowledge on ACChair', async () => {
    await setup(['ACChair'], [actionItem({ status: 'closed' })]);
    expect(component.canAcknowledge()).toBe(true);
  });

  it('closes an item with a closure response (CIA)', async () => {
    await setup(['CIA'], [actionItem({ status: 'in_progress' })]);
    // Drive the PATCH directly; the close dialog supplies the response text.
    component['service']
      .updateActionItem('a-1', { closureResponse: 'Done.' })
      .subscribe();
    const req = http.expectOne(`${BASE}/ac-action-items/a-1`);
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body.closureResponse).toBe('Done.');
    req.flush({ data: actionItem({ status: 'closed', closureResponse: 'Done.' }) });
  });

  it('re-queries with the status filter at page 1', async () => {
    await setup(['ACMember'], [actionItem()]);
    component.onStatusChange('closed');
    const req = http.expectOne((r) => r.url === `${BASE}/ac-action-items`);
    expect(req.request.params.get('status')).toBe('closed');
    expect(req.request.params.get('page')).toBe('1');
    req.flush({ data: page([]) });
    await fixture.whenStable();
    // The reload re-attempts the directory fetch (cache still empty from setup).
    http
      .expectOne((r) => r.url === `${BASE}/users`)
      .flush({ data: emptyUserPage() });
    await fixture.whenStable();
    expect(component.items().length).toBe(0);
  });

  it('navigates to another page via the paginator, carrying page + pageSize', async () => {
    await setup(['ACMember'], [actionItem()]);
    component.onPageChange(2);
    const req = http.expectOne((r) => r.url === `${BASE}/ac-action-items`);
    expect(req.request.params.get('page')).toBe('2');
    expect(req.request.params.get('pageSize')).toBe('25');
    req.flush({ data: page([actionItem({ id: 'a-2' })], 50, 2) });
    await fixture.whenStable();
    // The reload re-attempts the directory fetch (cache still empty from setup).
    http
      .expectOne((r) => r.url === `${BASE}/users`)
      .flush({ data: emptyUserPage() });
    await fixture.whenStable();
    expect(component.page()).toBe(2);
    expect(component.total()).toBe(50);
  });
});