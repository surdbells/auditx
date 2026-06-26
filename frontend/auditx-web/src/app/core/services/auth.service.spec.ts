import { TestBed } from '@angular/core/testing';
import { provideZonelessChangeDetection } from '@angular/core';
import {
  HttpTestingController,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { provideHttpClient } from '@angular/common/http';

import { AuthService } from './auth.service';
import { SessionDto } from '../models';

const BASE = '/api/v1';

function makeSession(overrides: Partial<SessionDto> = {}): SessionDto {
  return {
    userId: 'u-1',
    email: 'jane@bank.example',
    firstName: 'Jane',
    lastName: 'Auditor',
    displayName: 'Jane Auditor',
    status: 'active',
    roles: ['Auditor'],
    permissions: ['ManageUsers'],
    expiresAt: '2030-01-01T00:00:00Z',
    absoluteExpiresAt: '2030-01-02T00:00:00Z',
    ...overrides,
  };
}

describe('AuthService', () => {
  let service: AuthService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideZonelessChangeDetection(),
        provideHttpClient(),
        provideHttpClientTesting(),
        AuthService,
      ],
    });
    service = TestBed.inject(AuthService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('starts unauthenticated', () => {
    expect(service.isAuthenticated()).toBe(false);
    expect(service.session()).toBeNull();
  });

  it('login sets the session signal and marks it loaded', () => {
    const session = makeSession();
    let result: SessionDto | undefined;

    service
      .login({ username: 'jane', password: 'secret' })
      .subscribe((s) => (result = s));

    const req = http.expectOne(`${BASE}/auth/login`);
    expect(req.request.method).toBe('POST');
    expect(req.request.withCredentials).toBe(true);
    req.flush({ data: session });

    expect(result).toEqual(session);
    expect(service.isAuthenticated()).toBe(true);
    expect(service.session()).toEqual(session);
    expect(service.sessionLoaded()).toBe(true);
    expect(service.displayName()).toBe('Jane Auditor');
  });

  it('hasPermission reflects the session permission list', () => {
    service.setSession(makeSession({ permissions: ['ManageRoles', 'ViewAudit'] }));
    expect(service.hasPermission('ManageRoles')).toBe(true);
    expect(service.hasPermission('ManageUsers')).toBe(false);
    expect(service.hasAnyPermission('Nope', 'ViewAudit')).toBe(true);
    expect(service.hasAnyPermission('Nope', 'Nada')).toBe(false);
  });

  it('awaitingRole is true only for awaiting_role_assignment status', () => {
    service.setSession(makeSession({ status: 'awaiting_role_assignment' }));
    expect(service.awaitingRole()).toBe(true);
    service.setSession(makeSession({ status: 'active' }));
    expect(service.awaitingRole()).toBe(false);
  });

  it('logout clears the session', () => {
    service.setSession(makeSession());
    expect(service.isAuthenticated()).toBe(true);

    service.logout().subscribe();
    const req = http.expectOne(`${BASE}/auth/logout`);
    expect(req.request.method).toBe('POST');
    req.flush(null, { status: 204, statusText: 'No Content' });

    expect(service.isAuthenticated()).toBe(false);
    expect(service.session()).toBeNull();
  });

  it('loadSession marks loaded even on 401', () => {
    let errored = false;
    service.loadSession().subscribe({ error: () => (errored = true) });

    const req = http.expectOne(`${BASE}/auth/session`);
    req.flush({ title: 'Unauthorized' }, { status: 401, statusText: 'Unauthorized' });

    expect(errored).toBe(true);
    expect(service.isAuthenticated()).toBe(false);
    expect(service.sessionLoaded()).toBe(true);
  });
});
