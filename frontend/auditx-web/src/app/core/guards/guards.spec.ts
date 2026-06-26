import { TestBed } from '@angular/core/testing';
import {
  EnvironmentInjector,
  provideZonelessChangeDetection,
  runInInjectionContext,
} from '@angular/core';
import { SessionDto } from '../models';
import {
  ActivatedRouteSnapshot,
  RouterStateSnapshot,
  UrlTree,
} from '@angular/router';
import { of } from 'rxjs';

import { authGuard } from './auth.guard';
import { permissionGuard } from './permission.guard';
import { AuthService } from '../services/auth.service';
import { NotificationService } from '../services/notification.service';

function state(url: string): RouterStateSnapshot {
  return { url } as RouterStateSnapshot;
}

const route = {} as ActivatedRouteSnapshot;

describe('authGuard', () => {
  let auth: jasmine.SpyObj<AuthService>;
  let injector: EnvironmentInjector;

  beforeEach(() => {
    auth = jasmine.createSpyObj<AuthService>(
      'AuthService',
      ['isAuthenticated', 'awaitingRole', 'sessionLoaded', 'loadSession'],
    );

    TestBed.configureTestingModule({
      providers: [
        provideZonelessChangeDetection(),
        { provide: AuthService, useValue: auth },
      ],
    });
    injector = TestBed.inject(EnvironmentInjector);
  });

  it('redirects unauthenticated users to /login with returnUrl', () => {
    auth.sessionLoaded.and.returnValue(true);
    auth.isAuthenticated.and.returnValue(false);

    const result = runInInjectionContext(injector, () =>
      authGuard(route, state('/dashboard')),
    ) as UrlTree;

    expect(result instanceof UrlTree).toBe(true);
    expect(result.toString()).toContain('/login');
    expect(result.toString()).toContain('returnUrl');
  });

  it('allows an authenticated, role-bearing user', () => {
    auth.sessionLoaded.and.returnValue(true);
    auth.isAuthenticated.and.returnValue(true);
    auth.awaitingRole.and.returnValue(false);

    const result = runInInjectionContext(injector, () =>
      authGuard(route, state('/dashboard')),
    );

    expect(result).toBe(true);
  });

  it('routes awaiting-role users to /awaiting-role', () => {
    auth.sessionLoaded.and.returnValue(true);
    auth.isAuthenticated.and.returnValue(true);
    auth.awaitingRole.and.returnValue(true);

    const result = runInInjectionContext(injector, () =>
      authGuard(route, state('/dashboard')),
    ) as UrlTree;

    expect(result.toString()).toContain('/awaiting-role');
  });

  it('loads the session first when not yet probed', () => {
    auth.sessionLoaded.and.returnValue(false);
    auth.loadSession.and.returnValue(of({} as SessionDto));
    auth.isAuthenticated.and.returnValue(true);
    auth.awaitingRole.and.returnValue(false);

    const result = runInInjectionContext(injector, () =>
      authGuard(route, state('/dashboard')),
    );

    let value: unknown;
    (result as { subscribe: (cb: (v: unknown) => void) => void }).subscribe(
      (v) => (value = v),
    );
    expect(auth.loadSession).toHaveBeenCalled();
    expect(value).toBe(true);
  });
});

describe('permissionGuard', () => {
  let auth: jasmine.SpyObj<AuthService>;
  let notify: jasmine.SpyObj<NotificationService>;
  let injector: EnvironmentInjector;

  beforeEach(() => {
    auth = jasmine.createSpyObj<AuthService>(
      'AuthService',
      ['isAuthenticated', 'hasAnyPermission', 'sessionLoaded', 'loadSession'],
    );
    notify = jasmine.createSpyObj<NotificationService>('NotificationService', [
      'warning',
    ]);

    TestBed.configureTestingModule({
      providers: [
        provideZonelessChangeDetection(),
        { provide: AuthService, useValue: auth },
        { provide: NotificationService, useValue: notify },
      ],
    });
    injector = TestBed.inject(EnvironmentInjector);
  });

  it('allows when the user holds the required permission', () => {
    auth.sessionLoaded.and.returnValue(true);
    auth.isAuthenticated.and.returnValue(true);
    auth.hasAnyPermission.and.returnValue(true);

    const guard = permissionGuard('ManageUsers');
    const result = runInInjectionContext(injector, () =>
      guard(route, state('/admin/users')),
    );

    expect(result).toBe(true);
    expect(auth.hasAnyPermission).toHaveBeenCalledWith('ManageUsers');
  });

  it('denies and redirects to /dashboard with a warning when lacking permission', () => {
    auth.sessionLoaded.and.returnValue(true);
    auth.isAuthenticated.and.returnValue(true);
    auth.hasAnyPermission.and.returnValue(false);

    const guard = permissionGuard('ManageRoles');
    const result = runInInjectionContext(injector, () =>
      guard(route, state('/admin/roles')),
    ) as UrlTree;

    expect(result.toString()).toContain('/dashboard');
    expect(notify.warning).toHaveBeenCalled();
  });
});
