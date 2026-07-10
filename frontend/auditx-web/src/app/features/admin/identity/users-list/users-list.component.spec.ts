import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';

import { UsersListComponent } from './users-list.component';
import { provideTestEnv } from '../../../../../testing/test-providers';
import { PagedResult, RoleDto, UserDto } from '../../../../core/models';

const BASE = '/api/v1';

function user(id: string, name: string): UserDto {
  return {
    id,
    email: `${name}@bank.example`,
    firstName: name,
    lastName: 'User',
    displayName: name,
    status: 'active',
    lastLoginAt: null,
  };
}

function page(
  items: UserDto[],
  total = items.length,
  pageNum = 1,
  pageSize = 25,
): PagedResult<UserDto> {
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

describe('UsersListComponent', () => {
  let fixture: ComponentFixture<UsersListComponent>;
  let component: UsersListComponent;
  let http: HttpTestingController;

  function flushInitial(users: UserDto[], roles: RoleDto[] = [], total = users.length): void {
    // roles request (filter dropdown)
    http.expectOne((r) => r.url === `${BASE}/roles`).flush({ data: roles });
    // first page of users
    http.expectOne((r) => r.url === `${BASE}/users`).flush({ data: page(users, total) });
  }

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [UsersListComponent],
      providers: [provideTestEnv(), provideRouter([])],
    }).compileComponents();

    fixture = TestBed.createComponent(UsersListComponent);
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  it('loads and renders users in the table', async () => {
    flushInitial([user('1', 'Alice'), user('2', 'Bob')]);
    await fixture.whenStable();
    fixture.detectChanges();

    expect(component.users().length).toBe(2);
    expect(component.total()).toBe(2);
    expect(component.state()).toBe('ready');

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Alice');
    expect(text).toContain('Bob');
  });

  it('shows the empty state when no users are returned', async () => {
    flushInitial([]);
    await fixture.whenStable();
    fixture.detectChanges();

    expect(component.isEmpty()).toBe(true);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('No users found');
  });

  it('navigates to another page via the paginator, carrying page + pageSize', async () => {
    flushInitial([user('1', 'Alice')], [], 50);
    await fixture.whenStable();
    fixture.detectChanges();

    component.onPageChange(2);
    const req = http.expectOne((r) => r.url === `${BASE}/users`);
    expect(req.request.params.get('page')).toBe('2');
    expect(req.request.params.get('pageSize')).toBe('25');
    req.flush({ data: page([user('2', 'Bob')], 50, 2) });

    expect(component.users().map((u) => u.id)).toEqual(['2']);
    expect(component.page()).toBe(2);
  });

  it('enters the error state when the first page fails', async () => {
    http.expectOne((r) => r.url === `${BASE}/roles`).flush({ data: [] });
    http
      .expectOne((r) => r.url === `${BASE}/users`)
      .flush({ title: 'Boom' }, { status: 500, statusText: 'Server Error' });

    await fixture.whenStable();
    fixture.detectChanges();
    expect(component.state()).toBe('error');
  });
});
