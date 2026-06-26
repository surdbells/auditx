import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { MatDialog, MatDialogRef } from '@angular/material/dialog';
import { of } from 'rxjs';

import { PlanDetailComponent } from './plan-detail.component';
import { provideTestEnv } from '../../../../testing/test-providers';
import { AuthService } from '../../../core/services/auth.service';
import { NotificationService } from '../../../core/services/notification.service';
import {
  Plan,
  PlanExecution,
  PlanStatus,
  SessionDto,
} from '../../../core/models';

const BASE = '/api/v1';

function plan(status: PlanStatus = 'draft', overrides: Partial<Plan> = {}): Plan {
  return {
    id: 'p-1',
    periodLabel: 'FY2026',
    periodStart: '2026-01-01',
    periodEnd: '2026-12-31',
    status,
    submittedAt: null,
    approvedAt: null,
    approvalDecision: null,
    items: [],
    ...overrides,
  };
}

function execution(): PlanExecution {
  return {
    totalItems: 0,
    countsByStatus: {},
    percentComplete: 0,
    behindSchedule: [],
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

describe('PlanDetailComponent', () => {
  let fixture: ComponentFixture<PlanDetailComponent>;
  let component: PlanDetailComponent;
  let http: HttpTestingController;
  let notify: jasmine.SpyObj<NotificationService>;
  let dialog: jasmine.SpyObj<MatDialog>;

  async function setup(
    perms: string[],
    initial: Plan,
  ): Promise<void> {
    notify = jasmine.createSpyObj<NotificationService>('NotificationService', [
      'success',
      'info',
      'warning',
      'error',
    ]);
    dialog = jasmine.createSpyObj<MatDialog>('MatDialog', ['open']);

    TestBed.configureTestingModule({
      imports: [PlanDetailComponent],
      providers: [
        provideTestEnv(),
        provideRouter([]),
        { provide: NotificationService, useValue: notify },
        { provide: MatDialog, useValue: dialog },
      ],
    });
    const auth = TestBed.inject(AuthService);
    auth.setSession(session(perms));

    fixture = TestBed.createComponent(PlanDetailComponent);
    fixture.componentRef.setInput('id', 'p-1');
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();

    // fetch runs in a microtask
    await fixture.whenStable();
    http.expectOne(`${BASE}/annual-plans/p-1`).flush({ data: initial });
    await fixture.whenStable();
    http.expectOne(`${BASE}/annual-plans/p-1/execution`).flush({ data: execution() });
    fixture.detectChanges();
  }

  afterEach(() => http.verify());

  it('loads the plan and its execution roll-up', async () => {
    await setup(['ManagePlan'], plan('draft'));
    expect(component.state()).toBe('ready');
    expect(component.plan()?.periodLabel).toBe('FY2026');
    expect(component.execution()?.totalItems).toBe(0);
  });

  it('submits a draft plan and updates status', async () => {
    await setup(['ManagePlan'], plan('draft'));
    expect(component.canSubmit()).toBe(true);

    component.submit();

    const req = http.expectOne(`${BASE}/annual-plans/p-1/submit`);
    expect(req.request.method).toBe('POST');
    req.flush({ data: plan('submitted') });
    // submit reloads execution
    http.expectOne(`${BASE}/annual-plans/p-1/execution`).flush({ data: execution() });

    expect(component.plan()?.status).toBe('submitted');
    expect(notify.success).toHaveBeenCalled();
  });

  it('hides the decision action without the ACChair permission', async () => {
    await setup(['ManagePlan'], plan('submitted'));
    expect(component.canDecide()).toBe(false);
    expect(component.canDecideNow()).toBe(false);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).not.toContain('Decision');
  });

  it('shows and runs the decision action for an AC Chair on a submitted plan', async () => {
    await setup(['ManagePlan', 'ACChair'], plan('submitted'));
    expect(component.canDecideNow()).toBe(true);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Decision');

    dialog.open.and.returnValue({
      afterClosed: () => of({ decision: 'approved', comments: 'LGTM' }),
    } as MatDialogRef<unknown>);

    component.decide();

    const req = http.expectOne(`${BASE}/annual-plans/p-1/decision`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.decision).toBe('approved');
    req.flush({ data: plan('approved') });

    expect(component.plan()?.status).toBe('approved');
    expect(notify.success).toHaveBeenCalled();
  });

  it('does not allow an AC Chair to decide a draft plan', async () => {
    await setup(['ACChair'], plan('draft'));
    expect(component.canDecideNow()).toBe(false);
  });
});
