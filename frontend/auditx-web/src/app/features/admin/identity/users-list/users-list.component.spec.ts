import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';

import { UsersListComponent } from './users-list.component';
import { provideTestEnv } from '../../../../../testing/test-providers';
import { CursorPage, RoleDto, UserDto } from '../../../../core/models';

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

function page(items: UserDto[], hasMore = false): CursorPage<UserDto> {
  return { items, nextCursor: hasMore ? 'cursor-2' : null, hasMore };
}

describe('UsersListComponent', () => {
  let fixture: ComponentFixture<UsersListComponent>;
  let component: UsersListComponent;
  let http: HttpTestingController;

  function flushInitial(users: UserDto[], roles: RoleDto[] = [], hasMore = false): void {
    // roles request (filter dropdown)
    http.expectOne((r) => r.url === `${BASE}/roles`).flush({ data: roles });
    // first page of users
    http.expectOne((r) => r.url === `${BASE}/users`).flush({ data: page(users, hasMore) });
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

  it('appends results when loadMore is invoked', async () => {
    flushInitial([user('1', 'Alice')], [], true);
    await fixture.whenStable();
    fixture.detectChanges();

    expect(component.hasMore()).toBe(true);

    component.loadMore();
    const req = http.expectOne((r) => r.url === `${BASE}/users`);
    expect(req.request.params.get('cursor')).toBe('cursor-2');
    req.flush({ data: page([user('2', 'Bob')], false) });

    expect(component.users().length).toBe(2);
    expect(component.hasMore()).toBe(false);
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
