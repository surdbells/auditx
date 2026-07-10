import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { MatDialog, MatDialogRef } from '@angular/material/dialog';
import { of } from 'rxjs';

import { ExceptionDetailComponent } from './exception-detail.component';
import { provideTestEnv } from '../../../../testing/test-providers';
import { AuthService } from '../../../core/services/auth.service';
import { NotificationService } from '../../../core/services/notification.service';
import { Exception, ExceptionStatus, SessionDto } from '../../../core/models';

const BASE = '/api/v1';

function exception(
  status: ExceptionStatus = 'open',
  overrides: Partial<Exception> = {},
): Exception {
  return {
    id: 'x-1',
    auditId: 'a-1',
    checklistItemId: 'i-1',
    auditableEntityId: null,
    title: 'Missing control',
    severity: 'high',
    status,
    rootCause: 'No SoD',
    recommendation: 'Add SoD',
    category: null,
    ownerUserId: 'u-owner',
    raisedByUserId: 'u-raiser',
    raisedAt: '2026-01-01T00:00:00Z',
    targetDate: '2026-02-01',
    targetDateOverridden: false,
    isRecurrence: false,
    recurrenceOfExceptionId: null,
    ciaPending: false,
    isOverdue: false,
    daysPastTarget: 0,
    version: 'v1',
    mapActions: [],
    ...overrides,
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

describe('ExceptionDetailComponent', () => {
  let fixture: ComponentFixture<ExceptionDetailComponent>;
  let component: ExceptionDetailComponent;
  let http: HttpTestingController;
  let notify: jasmine.SpyObj<NotificationService>;
  let dialog: jasmine.SpyObj<MatDialog>;

  async function setup(perms: string[], ex: Exception): Promise<void> {
    notify = jasmine.createSpyObj<NotificationService>('NotificationService', [
      'success',
      'info',
      'warning',
      'error',
    ]);
    dialog = jasmine.createSpyObj<MatDialog>('MatDialog', ['open']);

    TestBed.configureTestingModule({
      imports: [ExceptionDetailComponent],
      providers: [
        provideTestEnv(),
        provideRouter([]),
        { provide: NotificationService, useValue: notify },
        { provide: MatDialog, useValue: dialog },
      ],
    });
    const auth = TestBed.inject(AuthService);
    auth.setSession(session(perms));

    fixture = TestBed.createComponent(ExceptionDetailComponent);
    fixture.componentRef.setInput('id', 'x-1');
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    await fixture.whenStable();

    http.expectOne(`${BASE}/exceptions/x-1`).flush({ data: ex });
    await fixture.whenStable();
    http.expectOne((r) => r.url === `${BASE}/users`).flush({
      data: {
        items: [
          { id: 'u-owner', displayName: 'Olive Owner' },
          { id: 'u-raiser', displayName: 'Ray Raiser' },
        ],
        nextCursor: null,
        hasMore: false,
      },
    });
    // P1-B: the detail page also loads the finding's control/regulation links.
    http.expectOne(`${BASE}/exceptions/x-1/links`).flush({ data: { controls: [], regulations: [] } });
    fixture.detectChanges();
    await fixture.whenStable();
  }

  afterEach(() => http.verify());

  it('loads the exception and resolves owner / raised-by names', async () => {
    await setup(['ViewExceptions'], exception('open'));
    expect(component.state()).toBe('ready');
    expect(component.exception()?.title).toBe('Missing control');
    expect(component.ownerName()).toBe('Olive Owner');
    expect(component.raisedByName()).toBe('Ray Raiser');
  });

  it('allows submitting a MAP only when open or rejected with SubmitMap', async () => {
    await setup(['ViewExceptions', 'SubmitMap'], exception('open'));
    expect(component.canSubmitMapNow()).toBe(true);

    fixture.componentRef.setInput('id', 'x-1');
    component.exception.set(exception('map_submitted'));
    expect(component.canSubmitMapNow()).toBe(false);

    component.exception.set(exception('map_rejected'));
    expect(component.canSubmitMapNow()).toBe(true);
  });

  it('submits a MAP carrying the version, refreshing local state', async () => {
    await setup(['ViewExceptions', 'SubmitMap'], exception('open'));

    dialog.open.and.returnValue({
      afterClosed: () =>
        of({
          actions: [
            { description: 'fix', ownerUserId: 'u-owner', targetDate: '2026-03-01' },
          ],
        }),
    } as MatDialogRef<unknown>);

    component.submitMap();

    const req = http.expectOne(`${BASE}/exceptions/x-1/map`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.actions[0].description).toBe('fix');
    expect(req.request.body.version).toBe('v1');
    req.flush({ data: exception('map_submitted', { version: 'v2' }) });

    expect(component.exception()?.status).toBe('map_submitted');
    expect(component.exception()?.version).toBe('v2');
    expect(notify.success).toHaveBeenCalled();
  });

  it('handles the 202 maker-checker-gated approve path', async () => {
    await setup(['ViewExceptions', 'ApproveMap'], exception('map_submitted'));
    expect(component.canReviewMap()).toBe(true);

    component.approveMap();

    const req = http.expectOne(`${BASE}/exceptions/x-1/map/approve`);
    expect(req.request.body.version).toBe('v1');
    req.flush(
      { data: { pendingActionId: 'pa-9' } },
      { status: 202, statusText: 'Accepted' },
    );

    // Toast says submitted for approval; status stays map_submitted via reload.
    expect(notify.info).toHaveBeenCalled();
    http
      .expectOne(`${BASE}/exceptions/x-1`)
      .flush({ data: exception('map_submitted') });
    expect(component.exception()?.status).toBe('map_submitted');
  });

  it('applies the 200 approve path directly', async () => {
    await setup(['ViewExceptions', 'ApproveMap'], exception('map_submitted'));

    component.approveMap();

    http
      .expectOne(`${BASE}/exceptions/x-1/map/approve`)
      .flush({ data: exception('map_approved', { version: 'v2' }) });

    expect(component.exception()?.status).toBe('map_approved');
    expect(notify.success).toHaveBeenCalled();
  });

  it('reloads the exception on a concurrency conflict', async () => {
    await setup(['ViewExceptions', 'SubmitMap'], exception('open'));

    dialog.open.and.returnValue({
      afterClosed: () =>
        of({
          actions: [
            { description: 'fix', ownerUserId: 'u-owner', targetDate: '2026-03-01' },
          ],
        }),
    } as MatDialogRef<unknown>);

    component.submitMap();

    http
      .expectOne(`${BASE}/exceptions/x-1/map`)
      .flush(
        { error_code: 'exception.concurrency_conflict', title: 'Conflict' },
        { status: 409, statusText: 'Conflict' },
      );

    // Reloads to pick up the fresh version (users already cached).
    http
      .expectOne(`${BASE}/exceptions/x-1`)
      .flush({ data: exception('open', { version: 'v9' }) });

    expect(component.exception()?.version).toBe('v9');
  });

  it('can CIA-countersign only when ciaPending and holding CIA', async () => {
    await setup(['ViewExceptions', 'CIA'], exception('pending_closure', { ciaPending: true }));
    expect(component.canCiaCountersign()).toBe(true);

    component.ciaCountersign();
    http
      .expectOne(`${BASE}/exceptions/x-1/cia-countersign`)
      .flush({ data: exception('closed') });
    expect(component.exception()?.status).toBe('closed');
  });
});
