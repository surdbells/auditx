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
    version: 'v1',
    teamMembers: [],
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
      .list({ status: 'in_progress', auditType: 'AML', limit: 20 })
      .subscribe((page) => (result = page));
    const req = http.expectOne((r) => r.url === `${BASE}/audits`);
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('status')).toBe('in_progress');
    expect(req.request.params.get('auditType')).toBe('AML');
    req.flush({ data: { items: [], nextCursor: null, hasMore: false } });
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
});
