import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { MatDialog, MatDialogRef } from '@angular/material/dialog';
import { of } from 'rxjs';

import { ConfigurationDetailComponent } from './configuration-detail.component';
import { provideTestEnv } from '../../../../../testing/test-providers';
import { AuthService } from '../../../../core/services/auth.service';
import { NotificationService } from '../../../../core/services/notification.service';
import {
  ConfigurationVersion,
  ProblemDetails,
  SessionDto,
} from '../../../../core/models';

const BASE = '/api/v1';
const DOMAIN = 'exception_defaults';

const DEFINITION_JSON = JSON.stringify({
  target_days: { critical: 30, high: 60, medium: 90, low: 120 },
  recurrence_window_months: 12,
  recurrence_threshold: 3,
});

function version(
  overrides: Partial<ConfigurationVersion> = {},
): ConfigurationVersion {
  return {
    id: 'cv-1',
    domain: DOMAIN,
    versionNumber: 1,
    definitionJson: DEFINITION_JSON,
    isActive: true,
    changeReason: 'Initial seed.',
    createdByUserId: 'u-1',
    createdAtUtc: '2026-06-01T10:00:00Z',
    activatedBy: 'u-1',
    activatedAt: '2026-06-01T10:05:00Z',
    version: 'rv1',
    ...overrides,
  };
}

/** Offset-paginated version-history envelope payload. */
function historyPage(items: ConfigurationVersion[]): {
  items: ConfigurationVersion[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasPrevious: boolean;
  hasNext: boolean;
} {
  return {
    items,
    total: items.length,
    page: 1,
    pageSize: 25,
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

describe('ConfigurationDetailComponent', () => {
  let fixture: ComponentFixture<ConfigurationDetailComponent>;
  let component: ConfigurationDetailComponent;
  let http: HttpTestingController;
  let notify: jasmine.SpyObj<NotificationService>;
  let dialog: jasmine.SpyObj<MatDialog>;

  async function setup(
    perms: string[] = ['ViewConfig', 'ManageConfiguration'],
    versions: ConfigurationVersion[] = [version({ isActive: true })],
  ): Promise<void> {
    notify = jasmine.createSpyObj<NotificationService>('NotificationService', [
      'success',
      'info',
      'warning',
      'error',
    ]);
    dialog = jasmine.createSpyObj<MatDialog>('MatDialog', ['open']);

    TestBed.configureTestingModule({
      imports: [ConfigurationDetailComponent],
      providers: [
        provideTestEnv(),
        provideRouter([]),
        { provide: NotificationService, useValue: notify },
        { provide: MatDialog, useValue: dialog },
      ],
    });
    const auth = TestBed.inject(AuthService);
    auth.setSession(session(perms));

    fixture = TestBed.createComponent(ConfigurationDetailComponent);
    fixture.componentRef.setInput('domain', DOMAIN);
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    await fixture.whenStable();

    http.expectOne(`${BASE}/configurations/${DOMAIN}`).flush({
      data: version({ isActive: true }),
    });
    http
      .expectOne((r) => r.url === `${BASE}/configurations/${DOMAIN}/versions`)
      .flush({ data: historyPage(versions) });
    await fixture.whenStable();
    fixture.detectChanges();
  }

  afterEach(() => http.verify());

  it('loads the active version, parses its values, and lists history', async () => {
    await setup();
    expect(component.state()).toBe('ready');
    expect(component.activeDefinition()?.criticalTargetDays).toBe(30);
    expect(component.versions().length).toBe(1);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Active version');
  });

  it('saves a typed draft via POST and refreshes history', async () => {
    await setup();

    component.form.controls.changeReason.setValue(
      'Tightening the critical SLA to two weeks.',
    );
    component.saveDraft();

    const req = http.expectOne(`${BASE}/configurations/${DOMAIN}`);
    expect(req.request.method).toBe('POST');
    const parsed = JSON.parse(req.request.body.definitionJson);
    // The editor is pre-populated from the active version's values.
    expect(parsed.target_days.critical).toBe(30);
    expect(parsed.recurrence_threshold).toBe(3);
    req.flush({ data: version({ versionNumber: 2, isActive: false }) });

    // history reload
    http
      .expectOne((r) => r.url === `${BASE}/configurations/${DOMAIN}/versions`)
      .flush({ data: historyPage([]) });

    expect(notify.success).toHaveBeenCalled();
  });

  it('surfaces inline 422 field errors when a draft is rejected', async () => {
    await setup();

    component.form.controls.changeReason.setValue(
      'A perfectly valid reason of sufficient length.',
    );
    component.saveDraft();

    const problem: ProblemDetails = {
      title: 'Validation failed',
      status: 422,
      field_errors: [
        {
          field: 'recurrence_threshold',
          code: 'min',
          message: 'Threshold must be at least 2.',
        },
      ],
    };
    http
      .expectOne(`${BASE}/configurations/${DOMAIN}`)
      .flush(problem, { status: 422, statusText: 'Unprocessable Entity' });

    expect(component.fieldErrors().length).toBe(1);
    expect(component.fieldErrors()[0].field).toBe('recurrence_threshold');
    fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Threshold must be at least 2.');
  });

  it('shows a pending banner on a 202 activate', async () => {
    await setup(['ViewConfig', 'ManageConfiguration'], [
      version({ versionNumber: 2, isActive: false }),
    ]);

    dialog.open.and.returnValue({
      afterClosed: () => of({ changeReason: 'Approved at the AC meeting.' }),
    } as MatDialogRef<unknown>);

    component.activate(version({ versionNumber: 2, isActive: false }));

    http
      .expectOne(`${BASE}/configurations/${DOMAIN}/versions/2/activate`)
      .flush(
        { data: { pendingActionId: 'pa-7' } },
        { status: 202, statusText: 'Accepted' },
      );

    expect(component.pendingBanner()).toContain('pa-7');
    expect(notify.info).toHaveBeenCalled();
  });

  it('refreshes the active version on a 200 activate', async () => {
    await setup(['ViewConfig', 'ManageConfiguration'], [
      version({ versionNumber: 2, isActive: false }),
    ]);

    dialog.open.and.returnValue({
      afterClosed: () => of({ changeReason: 'Approved at the AC meeting.' }),
    } as MatDialogRef<unknown>);

    component.activate(version({ versionNumber: 2, isActive: false }));

    http
      .expectOne(`${BASE}/configurations/${DOMAIN}/versions/2/activate`)
      .flush({ data: version({ versionNumber: 2, isActive: true }) });
    // history reload
    http
      .expectOne((r) => r.url === `${BASE}/configurations/${DOMAIN}/versions`)
      .flush({ data: historyPage([]) });

    expect(component.active()?.versionNumber).toBe(2);
    expect(notify.success).toHaveBeenCalled();
  });

  it('warns when rolling back to the already-active version (409)', async () => {
    await setup(['ViewConfig', 'ManageConfiguration'], [
      version({ versionNumber: 1, isActive: true }),
    ]);

    dialog.open.and.returnValue({
      afterClosed: () => of({ changeReason: 'Reverting to the prior values.' }),
    } as MatDialogRef<unknown>);

    component.rollback(version({ versionNumber: 1, isActive: true }));

    http
      .expectOne(`${BASE}/configurations/${DOMAIN}/rollback`)
      .flush(
        { title: 'Already active', status: 409 },
        { status: 409, statusText: 'Conflict' },
      );

    expect(notify.warning).toHaveBeenCalled();
  });

  it('hides write actions without ManageConfiguration', async () => {
    await setup(['ViewConfig']);
    expect(component.canManage()).toBe(false);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).not.toContain('New draft');
  });
});
