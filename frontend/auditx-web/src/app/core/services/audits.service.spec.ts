import { TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';

import { AuditsService } from './audits.service';
import { provideTestEnv } from '../../../testing/test-providers';
import { Audit } from '../models';

const BASE = '/api/v1';

function audit(overrides: Partial<Audit> = {}): Audit {
  return {
    id: 'a-1',
    name: 'AML Review',
    scopeDescription: null,
    auditType: 'AML',
    status: 'draft',
    startDate: '2026-01-01',
    targetEndDate: '2026-03-01',
    actualEndDate: null,
    templateId: null,
    templateVersion: null,
    planItemId: null,
    leadUserId: 'u-lead',
    auditeeUserId: 'u-auditee',
    cancellationReason: null,
    budgetedHours: null,
    isSelfAssessment: false,
    version: 'v1',
    teamMembers: [],
    sections: [],
    checklistItems: [],
    ...overrides,
  };
}

describe('AuditsService', () => {
  let service: AuditsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideTestEnv()] });
    service = TestBed.inject(AuditsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists audits with status + type filters and unwraps the page', () => {
    let result: { items: unknown[] } | undefined;
    service
      .list({ status: 'in_progress', auditType: 'AML', page: 1, pageSize: 25 })
      .subscribe((page) => (result = page));
    const req = http.expectOne((r) => r.url === `${BASE}/audits`);
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('status')).toBe('in_progress');
    expect(req.request.params.get('auditType')).toBe('AML');
    expect(req.request.params.get('pageSize')).toBe('25');
    req.flush({ data: { items: [], total: 0, page: 1, pageSize: 25, totalPages: 1, hasPrevious: false, hasNext: false } });
    expect(result?.items.length).toBe(0);
  });

  it('fetches the status counts', () => {
    let result: { byStatus: Record<string, number> } | undefined;
    service.counts().subscribe((c) => (result = c));
    const req = http.expectOne(`${BASE}/audits/counts`);
    expect(req.request.method).toBe('GET');
    req.flush({ data: { byStatus: { draft: 2, planned: 1 } } });
    expect(result?.byStatus['draft']).toBe(2);
  });

  it('gets an audit and unwraps data', () => {
    let result: Audit | undefined;
    service.getById('a-1').subscribe((a) => (result = a));
    http.expectOne(`${BASE}/audits/a-1`).flush({ data: audit() });
    expect(result?.id).toBe('a-1');
  });

  it('creates an audit', () => {
    service
      .create({
        name: 'AML Review',
        auditType: 'AML',
        startDate: '2026-01-01',
        leadUserId: 'u-lead',
        auditeeUserId: 'u-auditee',
        backdatingOverride: false,
      })
      .subscribe();
    const req = http.expectOne(`${BASE}/audits`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.name).toBe('AML Review');
    req.flush({ data: audit() });
  });

  it('updates an audit carrying the version', () => {
    service
      .update('a-1', {
        name: 'Renamed',
        startDate: '2026-01-01',
        targetEndDate: '2026-03-01',
        version: 'v1',
      })
      .subscribe();
    const req = http.expectOne(`${BASE}/audits/a-1`);
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body.version).toBe('v1');
    req.flush({ data: audit({ version: 'v2' }) });
  });

  it('transitions an audit carrying the version', () => {
    service
      .transition('a-1', { targetState: 'planned', version: 'v1' })
      .subscribe();
    const req = http.expectOne(`${BASE}/audits/a-1/transition`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.targetState).toBe('planned');
    expect(req.request.body.version).toBe('v1');
    req.flush({ data: audit({ status: 'planned', version: 'v2' }) });
  });

  it('cancels an audit', () => {
    service.cancel('a-1', { reason: 'Scope dropped', version: 'v1' }).subscribe();
    const req = http.expectOne(`${BASE}/audits/a-1/cancel`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.reason).toBe('Scope dropped');
    req.flush({ data: audit({ status: 'cancelled' }) });
  });

  it('adds a team member', () => {
    service
      .addTeamMember('a-1', { userId: 'u-2', teamRole: 'auditor', version: 'v1' })
      .subscribe();
    const req = http.expectOne(`${BASE}/audits/a-1/team`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.teamRole).toBe('auditor');
    req.flush({ data: audit() });
  });

  it('removes a team member via a void DELETE with version in the query string', () => {
    let done = false;
    service.removeTeamMember('a-1', 'm-1', 'v 1+').subscribe(() => (done = true));
    const req = http.expectOne(
      `${BASE}/audits/a-1/team/m-1?version=v%201%2B`,
    );
    expect(req.request.method).toBe('DELETE');
    req.flush(null, { status: 204, statusText: 'No Content' });
    expect(done).toBe(true);
  });

  it('transfers the lead carrying the version', () => {
    service
      .transferLead('a-1', {
        newLeadUserId: 'u-2',
        removeOutgoing: true,
        version: 'v1',
      })
      .subscribe();
    const req = http.expectOne(`${BASE}/audits/a-1/transfer-lead`);
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body.newLeadUserId).toBe('u-2');
    expect(req.request.body.removeOutgoing).toBe(true);
    req.flush({ data: audit() });
  });

  it('adds a checklist item', () => {
    service
      .addChecklistItem('a-1', {
        prompt: 'Check controls',
        responseType: 'pass_fail_na',
        isRequired: true,
        version: 'v1',
      })
      .subscribe();
    const req = http.expectOne(`${BASE}/audits/a-1/checklist/items`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.prompt).toBe('Check controls');
    req.flush({ data: audit() });
  });

  it('updates a checklist item', () => {
    service
      .updateChecklistItem('a-1', 'i-1', {
        prompt: 'Updated',
        responseType: 'pass_fail_na',
        isRequired: false,
        version: 'v1',
      })
      .subscribe();
    const req = http.expectOne(`${BASE}/audits/a-1/checklist/items/i-1`);
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body.version).toBe('v1');
    req.flush({ data: audit() });
  });

  it('removes a checklist item via a void DELETE with version in the query string', () => {
    let done = false;
    service
      .removeChecklistItem('a-1', 'i-1', 'v1')
      .subscribe(() => (done = true));
    const req = http.expectOne(`${BASE}/audits/a-1/checklist/items/i-1?version=v1`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null, { status: 204, statusText: 'No Content' });
    expect(done).toBe(true);
  });

  /* ===================== M5 — Execution / Fieldwork ===================== */

  it('submits a response carrying the audit version, unwrapping the ChecklistResponse', () => {
    let result: { id: string } | undefined;
    service
      .submitResponse('a-1', 'i-1', {
        verdict: 'fail',
        comment: 'Missing control',
        isDraft: false,
        version: 'v1',
      })
      .subscribe((r) => (result = r));
    const req = http.expectOne(`${BASE}/audits/a-1/items/i-1/responses`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.verdict).toBe('fail');
    expect(req.request.body.isDraft).toBe(false);
    expect(req.request.body.version).toBe('v1');
    req.flush({ data: { id: 'r-1', checklistItemId: 'i-1' } });
    expect(result?.id).toBe('r-1');
  });

  it('gets the current response for an item', () => {
    let result: { id: string } | null | undefined;
    service.getResponse('a-1', 'i-1').subscribe((r) => (result = r));
    const req = http.expectOne(`${BASE}/audits/a-1/items/i-1/responses`);
    expect(req.request.method).toBe('GET');
    req.flush({ data: { id: 'r-1' } });
    expect(result?.id).toBe('r-1');
  });

  it('discards a draft via a void DELETE with version in the query string', () => {
    let done = false;
    service.discardDraft('a-1', 'i-1', 'v 1').subscribe(() => (done = true));
    const req = http.expectOne(
      `${BASE}/audits/a-1/items/i-1/responses/draft?version=v%201`,
    );
    expect(req.request.method).toBe('DELETE');
    req.flush(null, { status: 204, statusText: 'No Content' });
    expect(done).toBe(true);
  });

  it('fetches the response history', () => {
    let result: unknown[] | undefined;
    service
      .getResponseHistory('a-1', 'i-1')
      .subscribe((h) => (result = h));
    const req = http.expectOne(
      `${BASE}/audits/a-1/items/i-1/responses/history`,
    );
    expect(req.request.method).toBe('GET');
    req.flush({ data: [{ id: 'h-1', eventType: 'submitted' }] });
    expect(result?.length).toBe(1);
  });

  it('fetches the checklist progress', () => {
    let result: { totalItems: number } | undefined;
    service.getChecklistProgress('a-1').subscribe((p) => (result = p));
    const req = http.expectOne(`${BASE}/audits/a-1/checklist/progress`);
    expect(req.request.method).toBe('GET');
    req.flush({ data: { totalItems: 3, respondedItems: 1, items: [] } });
    expect(result?.totalItems).toBe(3);
  });

  it('assigns an item carrying the version', () => {
    service
      .assignItem('a-1', 'i-1', { assignedUserId: 'u-2', version: 'v1' })
      .subscribe();
    const req = http.expectOne(`${BASE}/audits/a-1/items/i-1/assignment`);
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body.assignedUserId).toBe('u-2');
    expect(req.request.body.version).toBe('v1');
    req.flush({ data: audit() });
  });

  it('bulk-reassigns items carrying the version', () => {
    service
      .bulkReassign('a-1', {
        assignments: [{ itemId: 'i-1', assigneeUserId: 'u-2' }],
        version: 'v1',
      })
      .subscribe();
    const req = http.expectOne(`${BASE}/audits/a-1/items/bulk-reassign`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.assignments[0].itemId).toBe('i-1');
    expect(req.request.body.version).toBe('v1');
    req.flush({ data: audit() });
  });

  it('fetches the fail-without-exception review list', () => {
    let result: { count: number } | undefined;
    service.getFailWithoutException('a-1').subscribe((r) => (result = r));
    const req = http.expectOne(
      `${BASE}/audits/a-1/review/fail-without-exception`,
    );
    expect(req.request.method).toBe('GET');
    req.flush({ data: { count: 1, items: [{ itemId: 'i-1', prompt: 'P' }] } });
    expect(result?.count).toBe(1);
  });

  it('fetches the review summary', () => {
    let result: { fail: number } | undefined;
    service.getReviewSummary('a-1').subscribe((s) => (result = s));
    const req = http.expectOne(`${BASE}/audits/a-1/review/summary`);
    expect(req.request.method).toBe('GET');
    req.flush({
      data: { totalItems: 3, responded: 3, pass: 1, fail: 1, na: 1, exceptions: 0 },
    });
    expect(result?.fail).toBe(1);
  });

  it('records a fail judgement carrying the version', () => {
    service
      .recordFailJudgement('a-1', 'i-1', {
        justification: 'Accepted risk',
        version: 'v1',
      })
      .subscribe();
    const req = http.expectOne(`${BASE}/audits/a-1/items/i-1/fail-judgement`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.justification).toBe('Accepted risk');
    expect(req.request.body.version).toBe('v1');
    req.flush({ data: audit() });
  });

  it('uploads evidence as multipart form data (no version) and unwraps', () => {
    let result: { id: string } | undefined;
    const file = new File(['hello'], 'proof.txt', { type: 'text/plain' });
    service
      .uploadEvidence('a-1', 'r-1', file)
      .subscribe((e) => (result = e));
    const req = http.expectOne(`${BASE}/audits/a-1/responses/r-1/evidence`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body instanceof FormData).toBe(true);
    expect((req.request.body as FormData).get('file')).toBe(file);
    expect(req.request.withCredentials).toBe(true);
    req.flush({ data: { id: 'e-1', originalFilename: 'proof.txt' } });
    expect(result?.id).toBe('e-1');
  });

  it('lists evidence for a response', () => {
    let result: unknown[] | undefined;
    service.listEvidence('a-1', 'r-1').subscribe((e) => (result = e));
    const req = http.expectOne(`${BASE}/audits/a-1/responses/r-1/evidence`);
    expect(req.request.method).toBe('GET');
    req.flush({ data: [{ id: 'e-1' }] });
    expect(result?.length).toBe(1);
  });

  it('downloads evidence as a blob', () => {
    let result: Blob | undefined;
    service.downloadEvidence('a-1', 'e-1').subscribe((b) => (result = b));
    const req = http.expectOne(`${BASE}/audits/a-1/evidence/e-1`);
    expect(req.request.method).toBe('GET');
    expect(req.request.responseType).toBe('blob');
    expect(req.request.withCredentials).toBe(true);
    req.flush(new Blob(['data']));
    expect(result instanceof Blob).toBe(true);
  });

  it('deletes evidence via a void DELETE with the reason in the query string', () => {
    let done = false;
    service
      .deleteEvidence('a-1', 'e-1', 'wrong file')
      .subscribe(() => (done = true));
    const req = http.expectOne(
      `${BASE}/audits/a-1/evidence/e-1?reason=wrong%20file`,
    );
    expect(req.request.method).toBe('DELETE');
    req.flush(null, { status: 204, statusText: 'No Content' });
    expect(done).toBe(true);
  });
});
