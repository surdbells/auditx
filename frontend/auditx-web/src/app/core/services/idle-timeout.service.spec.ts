import { TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';
import { MatDialog, MatDialogRef, MatDialogState } from '@angular/material/dialog';
import { Router, provideRouter } from '@angular/router';
import { Subject } from 'rxjs';

import { IdleTimeoutService } from './idle-timeout.service';
import { AuthService } from './auth.service';
import { BrandingService } from './branding.service';
import { NotificationService } from './notification.service';
import { provideTestEnv } from '../../../testing/test-providers';
import { SessionDto } from '../models';

function session(): SessionDto {
  return {
    userId: 'u1',
    email: 'a@b.c',
    firstName: 'A',
    lastName: 'B',
    displayName: 'A B',
    status: 'active',
    roles: [],
    permissions: [],
    expiresAt: '',
    absoluteExpiresAt: '',
  };
}

describe('IdleTimeoutService', () => {
  let service: IdleTimeoutService;
  let auth: AuthService;
  let branding: BrandingService;
  let dialog: jasmine.SpyObj<MatDialog>;
  let http: HttpTestingController;
  let afterClosed$: Subject<boolean | undefined>;
  let dialogRef: jasmine.SpyObj<MatDialogRef<unknown, boolean>>;

  beforeEach(() => {
    // The cross-tab activity beacon persists in localStorage across tests — clear it so one test's
    // "stay signed in" (which beacons) can't read as another-tab activity in the next test.
    localStorage.removeItem('auditx.idle.lastActivity');
    jasmine.clock().install();
    jasmine.clock().mockDate(new Date(2026, 0, 1, 9, 0, 0));

    afterClosed$ = new Subject<boolean | undefined>();
    dialogRef = jasmine.createSpyObj<MatDialogRef<unknown, boolean>>('MatDialogRef', ['close', 'afterClosed', 'getState']);
    dialogRef.afterClosed.and.returnValue(afterClosed$.asObservable());
    dialogRef.getState.and.returnValue(MatDialogState.OPEN);
    // Closing from the service's expiry path completes the afterClosed stream with no result.
    dialogRef.close.and.callFake(() => {
      dialogRef.getState.and.returnValue(MatDialogState.CLOSED);
      afterClosed$.next(undefined);
      afterClosed$.complete();
    });
    dialog = jasmine.createSpyObj<MatDialog>('MatDialog', ['open']);
    dialog.open.and.returnValue(dialogRef as MatDialogRef<unknown>);

    TestBed.configureTestingModule({
      providers: [
        provideTestEnv(),
        provideRouter([]),
        { provide: MatDialog, useValue: dialog },
        {
          provide: NotificationService,
          useValue: jasmine.createSpyObj<NotificationService>('NotificationService', ['success', 'info', 'warning', 'error']),
        },
      ],
    });

    service = TestBed.inject(IdleTimeoutService);
    auth = TestBed.inject(AuthService);
    branding = TestBed.inject(BrandingService);
    http = TestBed.inject(HttpTestingController);

    auth.setSession(session());
  });

  afterEach(() => {
    jasmine.clock().uninstall();
    http.verify();
  });

  it('never warns when the policy is disabled (0 minutes)', () => {
    branding.idleTimeoutMinutes.set(0);
    service.start();

    jasmine.clock().tick(30 * 60_000); // half an hour of idle ticks

    expect(dialog.open).not.toHaveBeenCalled();
  });

  it('warns after the idle period and signs out when the countdown expires', () => {
    const router = TestBed.inject(Router);
    const navigate = spyOn(router, 'navigate').and.resolveTo(true);
    branding.idleTimeoutMinutes.set(1);
    branding.idleWarningSeconds.set(30);
    service.start();

    // One idle minute → the warning opens.
    jasmine.clock().tick(60_000);
    expect(dialog.open).toHaveBeenCalledTimes(1);

    // Countdown runs out → the service closes the dialog and signs the user out.
    jasmine.clock().tick(31_000);
    const logout = http.expectOne((r) => r.url.endsWith('/auth/logout'));
    logout.flush({});
    expect(auth.session()).toBeNull();
    expect(navigate).toHaveBeenCalledWith(['/login']);
  });

  it('lets afterClosed decide when the user chooses during the final second (no expiry override)', () => {
    branding.idleTimeoutMinutes.set(1);
    branding.idleWarningSeconds.set(30);
    service.start();

    jasmine.clock().tick(60_000);
    expect(dialog.open).toHaveBeenCalledTimes(1);

    // The user clicked "Stay" — the dialog is mid-close animation (no longer OPEN) but afterClosed
    // has not emitted yet. An expiry tick in this window must NOT sign the user out.
    dialogRef.getState.and.returnValue(MatDialogState.CLOSING);
    jasmine.clock().tick(31_000);
    http.expectNone((r) => r.url.endsWith('/auth/logout'));

    // afterClosed finally lands with the user's choice.
    afterClosed$.next(true);
    afterClosed$.complete();
    expect(auth.session()).not.toBeNull();
  });

  it('resets the idle clock when the user chooses to stay signed in', () => {
    branding.idleTimeoutMinutes.set(1);
    branding.idleWarningSeconds.set(30);
    service.start();

    jasmine.clock().tick(60_000);
    expect(dialog.open).toHaveBeenCalledTimes(1);

    // "Stay signed in" → no logout; the next warning needs another full idle minute.
    afterClosed$.next(true);
    afterClosed$.complete();
    jasmine.clock().tick(59_000);
    expect(dialog.open).toHaveBeenCalledTimes(1);
    http.expectNone((r) => r.url.endsWith('/auth/logout'));
  });
});
