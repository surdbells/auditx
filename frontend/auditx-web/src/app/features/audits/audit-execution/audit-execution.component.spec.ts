import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';
import { MatDialog, MatDialogRef } from '@angular/material/dialog';
import { of } from 'rxjs';

import { AuditExecutionComponent } from './audit-execution.component';
import { provideTestEnv } from '../../../../testing/test-providers';
import { AuthService } from '../../../core/services/auth.service';
import { NotificationService } from '../../../core/services/notification.service';
import {
  Audit,
  AuditStatus,
  ChecklistProgress,
  ResponseVerdict,
  SessionDto,
} from '../../../core/models';

const BASE = '/api/v1';

function audit(status: AuditStatus = 'in_progress', overrides: Partial<Audit> = {}): Audit {
  return {
    id: 'a-1',
    name: 'AML Review',
    scopeDescription: null,
    auditType: 'AML',
    status,
    startDate: '2026-01-01',
    targetEndDate: null,
    actualEndDate: null,
    templateId: null,
    templateVersion: null,
    planItemId: null,
    leadUserId: 'u-lead',
    auditeeUserId: 'u-auditee',
    cancellationReason: null,
    budgetedHours: null,
    version: 'v1',
    teamMembers: [
      {
        id: 'm-1',
        userId: 'u-auditor',
        teamRole: 'auditor',
        isActive: true,
        addedAt: '2026-01-01T00:00:00Z',
        removedAt: null,
      },
    ],
    sections: [],
    checklistItems: [],
    ...overrides,
  };
}

function progress(): ChecklistProgress {
  return {
    totalItems: 2,
    respondedItems: 1,
    inProgressItems: 0,
    notStartedItems: 1,
    items: [
      {
        itemId: 'i-1',
        sectionName: 'Controls',
        orderIndex: 0,
        prompt: 'Verify SoD',
        itemState: 'responded',
        verdict: 'pass',
        isRequired: true,
        assignedUserId: 'u-auditor',
        hasException: false,
        responseType: 'pass_fail_na',
      },
      {
        itemId: 'i-2',
        sectionName: 'Controls',
        orderIndex: 1,
        prompt: 'Verify access',
        itemState: 'not_started',
        verdict: null,
        isRequired: false,
        assignedUserId: null,
        hasException: false,
        responseType: 'pass_fail_na',
      },
    ],
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

describe('AuditExecutionComponent', () => {
  let fixture: ComponentFixture<AuditExecutionComponent>;
  let component: AuditExecutionComponent;
  let http: HttpTestingController;
  let notify: jasmine.SpyObj<NotificationService>;
  let dialog: jasmine.SpyObj<MatDialog>;

  async function setup(
    perms: string[],
    a: Audit,
    failItems: {
      itemId: string;
      prompt: string;
      comment: string | null;
      assignedUserId: string | null;
    }[] = [],
  ): Promise<void> {
    notify = jasmine.createSpyObj<NotificationService>('NotificationService', [
      'success',
      'info',
      'warning',
      'error',
    ]);
    dialog = jasmine.createSpyObj<MatDialog>('MatDialog', ['open']);

    TestBed.configureTestingModule({
      imports: [AuditExecutionComponent],
      providers: [
        provideTestEnv(),
        { provide: NotificationService, useValue: notify },
        { provide: MatDialog, useValue: dialog },
      ],
    });
    const auth = TestBed.inject(AuthService);
    auth.setSession(session(perms));

    fixture = TestBed.createComponent(AuditExecutionComponent);
    fixture.componentRef.setInput('audit', a);
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    await fixture.whenStable();

    // The effect fires refresh() which loads progress + summary.
    http
      .expectOne(`${BASE}/audits/a-1/checklist/progress`)
      .flush({ data: progress() });
    http
      .expectOne(`${BASE}/audits/a-1/review/summary`)
      .flush({
        data: { totalItems: 2, responded: 1, pass: 1, fail: 0, na: 0, exceptions: 0 },
      });
    // The fail worklist loads for anyone who can raise an exception (or manage) while the checklist is live.
    if (
      (a.status === 'under_review' || a.status === 'in_progress') &&
      (perms.includes('ManageAudit') || perms.includes('RaiseException'))
    ) {
      for (const req of http.match(
        `${BASE}/audits/a-1/review/fail-without-exception`,
      )) {
        req.flush({ data: { count: failItems.length, items: failItems } });
      }
    }
    fixture.detectChanges();
    // The master-detail workspace auto-selects the first item and lazily loads its response.
    for (const req of http.match((r) => /\/audits\/a-1\/items\/.+\/responses$/.test(r.url))) {
      req.flush({ data: null });
    }
    // The item-work tabs (evidence requests + procedures, and time when permitted) load their lists;
    // the evidence panel also lazy-loads the document-type reference-data for its form. Drain tolerantly.
    for (const req of http.match(`${BASE}/audits/a-1/evidence-requests`)) {
      req.flush({ data: [] });
    }
    for (const req of http.match(`${BASE}/audits/a-1/procedures`)) {
      req.flush({ data: [] });
    }
    for (const req of http.match((r) => r.url === `${BASE}/reference-data/evidence_document_type`)) {
      req.flush({ data: [] });
    }
    // Time panel loads only when the viewer holds LogTime / ViewTimeEntries (none of these fixtures do).
    for (const req of http.match(`${BASE}/audits/a-1/time-entries`)) {
      req.flush({ data: [] });
    }
    for (const req of http.match(`${BASE}/audits/a-1/time-entries/summary`)) {
      req.flush({ data: { actualHours: 0, budgetedHours: null, varianceHours: null, percentConsumed: null, entryCount: 0, byCategory: [] } });
    }
    // Rendering a row's actor name triggers the directory-backed lookup — drain it if it fired.
    for (const req of http.match((r) => r.url === `${BASE}/users/directory`)) {
      req.flush({
        data: {
          items: [
            { id: 'u-auditor', displayName: 'Andy Auditor' },
            { id: 'u-lead', displayName: 'Lara Lead' },
          ],
          total: 2,
          page: 1,
          pageSize: 5000,
          totalPages: 1,
          hasPrevious: false,
          hasNext: false,
        },
      });
    }
  }

  afterEach(() => http.verify());

  it('loads progress and computes the responded percentage', async () => {
    await setup(['RespondItem', 'ViewAudit'], audit('in_progress'));
    expect(component.progress()?.totalItems).toBe(2);
    expect(component.progressPct()).toBe(50);
    expect(component.summary()?.pass).toBe(1);
  });

  it('groups progress items by section ordered by orderIndex', async () => {
    await setup(['RespondItem'], audit('in_progress'));
    const groups = component.progressGroups();
    expect(groups.length).toBe(1);
    expect(groups[0].name).toBe('Controls');
    expect(groups[0].items.map((i) => i.itemId)).toEqual(['i-1', 'i-2']);
  });

  it('allows responding only with RespondItem AND an in_progress audit', async () => {
    await setup(['RespondItem'], audit('in_progress'));
    expect(component.canRespond()).toBe(true);

    fixture.componentRef.setInput('audit', audit('under_review'));
    fixture.detectChanges();
    expect(component.canRespond()).toBe(false);
  });

  it('forbids responding without the RespondItem permission', async () => {
    await setup(['ViewAudit'], audit('in_progress'));
    expect(component.canRespond()).toBe(false);
  });

  it('submits a response carrying the version, then reloads + emits', async () => {
    await setup(['RespondItem'], audit('in_progress'));
    const reload = spyOn(component.reloadRequested, 'emit');

    dialog.open.and.returnValue({
      afterClosed: () =>
        of({
          verdict: 'fail' as ResponseVerdict,
          comment: 'Missing control',
          isDraft: false,
        }),
    } as MatDialogRef<unknown>);

    component.respond(progress().items[1]);

    // respond() first loads the current response for the dialog.
    http
      .expectOne(`${BASE}/audits/a-1/items/i-2/responses`)
      .flush({ data: null });

    const submit = http.expectOne(
      (r) =>
        r.url === `${BASE}/audits/a-1/items/i-2/responses` &&
        r.method === 'POST',
    );
    expect(submit.request.body.verdict).toBe('fail');
    expect(submit.request.body.isDraft).toBe(false);
    expect(submit.request.body.version).toBe('v1');
    submit.flush({ data: { id: 'r-9', checklistItemId: 'i-2', isDraft: false } });

    // Newly-saved response loads its (empty) evidence list.
    http.expectOne(`${BASE}/audits/a-1/responses/r-9/evidence`).flush({ data: [] });

    // afterMutation: success toast, parent reload, and a self-refresh.
    expect(notify.success).toHaveBeenCalled();
    expect(reload).toHaveBeenCalled();
    http
      .expectOne(`${BASE}/audits/a-1/checklist/progress`)
      .flush({ data: progress() });
    http
      .expectOne(`${BASE}/audits/a-1/review/summary`)
      .flush({
        data: { totalItems: 2, responded: 2, pass: 1, fail: 1, na: 0, exceptions: 1 },
      });
  });

  it('discards a draft carrying the version in the query string', async () => {
    await setup(['RespondItem'], audit('in_progress'));
    const reload = spyOn(component.reloadRequested, 'emit');

    component.discardDraft(progress().items[1]);

    const req = http.expectOne(
      `${BASE}/audits/a-1/items/i-2/responses/draft?version=v1`,
    );
    expect(req.request.method).toBe('DELETE');
    req.flush(null, { status: 204, statusText: 'No Content' });

    expect(notify.success).toHaveBeenCalled();
    expect(reload).toHaveBeenCalled();
    http
      .expectOne(`${BASE}/audits/a-1/checklist/progress`)
      .flush({ data: progress() });
    http
      .expectOne(`${BASE}/audits/a-1/review/summary`)
      .flush({
        data: { totalItems: 2, responded: 1, pass: 1, fail: 0, na: 0, exceptions: 0 },
      });
  });

  it('uploads evidence as multipart form data (no version)', async () => {
    await setup(['RespondItem', 'UploadEvidence'], audit('in_progress'));
    const input = {
      files: [new File(['x'], 'proof.txt', { type: 'text/plain' })],
      value: 'proof.txt',
    } as unknown as HTMLInputElement;

    component.uploadEvidence('r-1', input);

    const req = http.expectOne(`${BASE}/audits/a-1/responses/r-1/evidence`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body instanceof FormData).toBe(true);
    req.flush({ data: { id: 'e-1', originalFilename: 'proof.txt', sizeBytes: 1 } });

    expect(component.evidenceFor('r-1').length).toBe(1);
    expect(notify.success).toHaveBeenCalled();
  });

  it('shows the reviewer fail panel when managing under review', async () => {
    await setup(['ManageAudit'], audit('under_review'));
    expect(component.showFailWorklist()).toBe(true);

    await setupFresh(['ManageAudit'], audit('in_progress'));
    expect(component.showFailWorklist()).toBe(false);
  });

  it('shows the fail worklist to a non-manager auditor with RaiseException when failures need one', async () => {
    await setup(['RaiseException', 'ViewAudit'], audit('in_progress'), [
      { itemId: 'i-2', prompt: 'Q2', comment: null, assignedUserId: null },
    ]);
    // An auditor (no ManageAudit) still sees the worklist so they can raise directly — not just the reviewer.
    expect(component.showFailWorklist()).toBe(true);
    expect(component.failItems().length).toBe(1);
    expect(component.canManage()).toBe(false);
  });

  // Re-create the component in a single test that needs two states without a
  // second top-level setup (which would reconfigure the TestBed).
  async function setupFresh(perms: string[], a: Audit): Promise<void> {
    fixture.componentRef.setInput('audit', a);
    fixture.detectChanges();
    await fixture.whenStable();
  }
});
