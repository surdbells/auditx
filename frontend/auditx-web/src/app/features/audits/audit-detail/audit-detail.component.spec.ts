import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { MatDialog, MatDialogRef } from '@angular/material/dialog';
import { of } from 'rxjs';

import { AuditDetailComponent } from './audit-detail.component';
import { provideTestEnv } from '../../../../testing/test-providers';
import { AuthService } from '../../../core/services/auth.service';
import { NotificationService } from '../../../core/services/notification.service';
import {
  Audit,
  AuditChecklistItem,
  AuditStatus,
  AuditTeamMember,
  SessionDto,
} from '../../../core/models';

const BASE = '/api/v1';

function member(overrides: Partial<AuditTeamMember> = {}): AuditTeamMember {
  return {
    id: 'm-1',
    userId: 'u-auditor',
    teamRole: 'auditor',
    isActive: true,
    addedAt: '2026-01-01T00:00:00Z',
    removedAt: null,
    ...overrides,
  };
}

function checkItem(
  overrides: Partial<AuditChecklistItem> = {},
): AuditChecklistItem {
  return {
    id: 'i-1',
    sectionName: 'Controls',
    orderIndex: 0,
    prompt: 'Verify segregation of duties',
    referenceNotes: null,
    responseType: 'pass_fail_na',
    assignedUserId: null,
    isRequired: true,
    itemState: 'not_started',
    ...overrides,
  };
}

function audit(status: AuditStatus = 'draft', overrides: Partial<Audit> = {}): Audit {
  return {
    id: 'a-1',
    name: 'AML Review',
    scopeDescription: 'Wire transfers',
    auditType: 'AML',
    status,
    startDate: '2026-01-01',
    targetEndDate: '2026-03-01',
    actualEndDate: null,
    templateId: null,
    templateVersion: null,
    planItemId: null,
    leadUserId: 'u-lead',
    auditeeUserId: 'u-auditee',
    cancellationReason: null,
    version: 'v1',
    teamMembers: [],
    checklistItems: [],
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

describe('AuditDetailComponent', () => {
  let fixture: ComponentFixture<AuditDetailComponent>;
  let component: AuditDetailComponent;
  let http: HttpTestingController;
  let notify: jasmine.SpyObj<NotificationService>;
  let dialog: jasmine.SpyObj<MatDialog>;

  async function setup(perms: string[], initial: Audit): Promise<void> {
    notify = jasmine.createSpyObj<NotificationService>('NotificationService', [
      'success',
      'info',
      'warning',
      'error',
    ]);
    dialog = jasmine.createSpyObj<MatDialog>('MatDialog', ['open']);

    TestBed.configureTestingModule({
      imports: [AuditDetailComponent],
      providers: [
        provideTestEnv(),
        provideRouter([]),
        { provide: NotificationService, useValue: notify },
        { provide: MatDialog, useValue: dialog },
      ],
    });
    const auth = TestBed.inject(AuthService);
    auth.setSession(session(perms));

    fixture = TestBed.createComponent(AuditDetailComponent);
    fixture.componentRef.setInput('id', 'a-1');
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();

    // fetch runs in a microtask
    await fixture.whenStable();
    http.expectOne(`${BASE}/audits/a-1`).flush({ data: initial });
    await fixture.whenStable();
    // ensureUsers loads active users
    http
      .expectOne((r) => r.url === `${BASE}/users`)
      .flush({
        data: {
          items: [
            { id: 'u-lead', displayName: 'Lara Lead' },
            { id: 'u-auditee', displayName: 'Aiden Auditee' },
            { id: 'u-auditor', displayName: 'Andy Auditor' },
          ],
          nextCursor: null,
          hasMore: false,
        },
      });
    fixture.detectChanges();
  }

  afterEach(() => http.verify());

  it('loads the audit and resolves lead / auditee display names', async () => {
    await setup(['ManageAudit'], audit('draft'));
    expect(component.state()).toBe('ready');
    expect(component.audit()?.name).toBe('AML Review');
    expect(component.leadName()).toBe('Lara Lead');
    expect(component.auditeeName()).toBe('Aiden Auditee');
  });

  it('disables Plan until there is a checklist item and an auditor', async () => {
    await setup(['ManageAudit'], audit('draft'));
    expect(component.canPlan()).toBe(false);

    component.audit.set(
      audit('draft', {
        teamMembers: [member()],
        checklistItems: [checkItem()],
      }),
    );
    expect(component.canPlan()).toBe(true);
  });

  it('groups checklist items by section ordered by orderIndex', async () => {
    await setup(
      ['ManageAudit'],
      audit('draft', {
        checklistItems: [
          checkItem({ id: 'i-2', sectionName: 'Controls', orderIndex: 1, prompt: 'B' }),
          checkItem({ id: 'i-1', sectionName: 'Controls', orderIndex: 0, prompt: 'A' }),
          checkItem({ id: 'i-3', sectionName: 'Records', orderIndex: 2, prompt: 'C' }),
        ],
      }),
    );
    const groups = component.checklistGroups();
    expect(groups.length).toBe(2);
    expect(groups[0].name).toBe('Controls');
    expect(groups[0].items.map((i) => i.prompt)).toEqual(['A', 'B']);
    expect(groups[1].name).toBe('Records');
  });

  it('plans a ready draft, sending the current version, and updates state', async () => {
    await setup(
      ['ManageAudit'],
      audit('draft', {
        teamMembers: [member()],
        checklistItems: [checkItem()],
      }),
    );
    expect(component.canPlan()).toBe(true);

    component.plan();

    const req = http.expectOne(`${BASE}/audits/a-1/transition`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.targetState).toBe('planned');
    expect(req.request.body.version).toBe('v1');
    req.flush({
      data: audit('planned', {
        version: 'v2',
        teamMembers: [member()],
        checklistItems: [checkItem()],
      }),
    });

    // ensureUsers re-runs after the mutation using the cached users (no new HTTP).
    expect(component.audit()?.status).toBe('planned');
    expect(component.audit()?.version).toBe('v2');
    expect(notify.success).toHaveBeenCalled();
  });

  it('sends to review without a reason when there are no open items', async () => {
    await setup(
      ['ManageAudit'],
      audit('in_progress', {
        version: 'v3',
        checklistItems: [checkItem({ itemState: 'responded' })],
      }),
    );
    expect(component.openItemCount()).toBe(0);

    component.sendToReview();

    // No reason dialog when everything is answered.
    expect(dialog.open).not.toHaveBeenCalled();
    const req = http.expectOne(`${BASE}/audits/a-1/transition`);
    expect(req.request.body.targetState).toBe('under_review');
    expect(req.request.body.version).toBe('v3');
    req.flush({ data: audit('under_review', { version: 'v4' }) });
    expect(component.audit()?.status).toBe('under_review');
  });

  it('prompts for a reason on send-to-review when items are open', async () => {
    await setup(
      ['ManageAudit'],
      audit('in_progress', {
        version: 'v3',
        checklistItems: [checkItem({ itemState: 'not_started' })],
      }),
    );
    expect(component.openItemCount()).toBe(1);

    dialog.open.and.returnValue({
      afterClosed: () => of({ reason: 'Deadline' }),
    } as MatDialogRef<unknown>);

    component.sendToReview();

    expect(dialog.open).toHaveBeenCalled();
    const req = http.expectOne(`${BASE}/audits/a-1/transition`);
    expect(req.request.body.targetState).toBe('under_review');
    expect(req.request.body.reason).toBe('Deadline');
    req.flush({ data: audit('under_review', { version: 'v4' }) });
    expect(component.audit()?.status).toBe('under_review');
  });

  it('reloads the audit on a concurrency conflict', async () => {
    await setup(
      ['ManageAudit'],
      audit('draft', {
        teamMembers: [member()],
        checklistItems: [checkItem()],
      }),
    );

    component.plan();

    http
      .expectOne(`${BASE}/audits/a-1/transition`)
      .flush(
        { error_code: 'audit.concurrency_conflict', title: 'Conflict' },
        { status: 409, statusText: 'Conflict' },
      );

    // The detail page reloads to pick up the fresh version. Users are already
    // cached from setup, so no second /users request is issued.
    http
      .expectOne(`${BASE}/audits/a-1`)
      .flush({ data: audit('planned', { version: 'v9' }) });

    expect(component.audit()?.version).toBe('v9');
  });

  it('hides management actions without ManageAudit', async () => {
    await setup(['ViewAudit'], audit('draft'));
    expect(component.canManage()).toBe(false);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).not.toContain('Plan');
    expect(text).not.toContain('Add item');
  });
});
