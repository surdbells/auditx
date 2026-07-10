import { TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';

import { ExceptionsService } from './exceptions.service';
import { provideTestEnv } from '../../../testing/test-providers';
import { ApproveMapResult, Exception } from '../models';

const BASE = '/api/v1';

function exception(overrides: Partial<Exception> = {}): Exception {
  return {
    id: 'x-1',
    auditId: 'a-1',
    checklistItemId: 'i-1',
    auditableEntityId: null,
    title: 'Missing control',
    severity: 'high',
    status: 'open',
    rootCause: null,
    recommendation: null,
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
    reopenCount: 0,
    version: 'v1',
    mapActions: [],
    verifications: [],
    ...overrides,
  };
}

describe('ExceptionsService', () => {
  let service: ExceptionsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideTestEnv()] });
    service = TestBed.inject(ExceptionsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('raises an exception against an audit', () => {
    let result: Exception | undefined;
    service
      .raise('a-1', {
        checklistItemId: 'i-1',
        title: 'T',
        severity: 'high',
        rootCause: 'rc',
        recommendation: 'rec',
        ownerUserId: 'u-owner',
      })
      .subscribe((e) => (result = e));
    const req = http.expectOne(`${BASE}/audits/a-1/exceptions`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.checklistItemId).toBe('i-1');
    req.flush({ data: exception() });
    expect(result?.id).toBe('x-1');
  });

  it('lists exceptions for an audit', () => {
    let result: unknown[] | undefined;
    service.listForAudit('a-1', 'open').subscribe((r) => (result = r));
    const req = http.expectOne((r) => r.url === `${BASE}/audits/a-1/exceptions`);
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('status')).toBe('open');
    req.flush({ data: [{ id: 'x-1' }] });
    expect(result?.length).toBe(1);
  });

  it('lists exceptions across audits with filters and unwraps the page', () => {
    let result: { items: unknown[] } | undefined;
    service
      .list({ status: 'open', severity: 'high', overdue: true, recurrence: false, page: 1, pageSize: 25 })
      .subscribe((p) => (result = p));
    const req = http.expectOne((r) => r.url === `${BASE}/exceptions`);
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('status')).toBe('open');
    expect(req.request.params.get('severity')).toBe('high');
    expect(req.request.params.get('overdue')).toBe('true');
    expect(req.request.params.get('recurrence')).toBe('false');
    expect(req.request.params.get('pageSize')).toBe('25');
    req.flush({ data: { items: [], total: 0, page: 1, pageSize: 25, totalPages: 1, hasPrevious: false, hasNext: false } });
    expect(result?.items.length).toBe(0);
  });

  it('maps plan, audit, search and raised-date filters to query params', () => {
    service
      .list({
        plan: 'p-1',
        audit: 'a-1',
        search: 'wire',
        raisedFrom: '2026-01-01T00:00:00Z',
        raisedTo: '2026-03-31T23:59:59Z',
        page: 1,
        pageSize: 25,
      })
      .subscribe();
    const req = http.expectOne((r) => r.url === `${BASE}/exceptions`);
    expect(req.request.params.get('plan')).toBe('p-1');
    expect(req.request.params.get('audit')).toBe('a-1');
    expect(req.request.params.get('search')).toBe('wire');
    expect(req.request.params.get('raisedFrom')).toBe('2026-01-01T00:00:00Z');
    expect(req.request.params.get('raisedTo')).toBe('2026-03-31T23:59:59Z');
    req.flush({ data: { items: [], total: 0, page: 1, pageSize: 25, totalPages: 1, hasPrevious: false, hasNext: false } });
  });

  it('gets an exception by id', () => {
    let result: Exception | undefined;
    service.getById('x-1').subscribe((e) => (result = e));
    http.expectOne(`${BASE}/exceptions/x-1`).flush({ data: exception() });
    expect(result?.id).toBe('x-1');
  });

  it('fetches the exception history', () => {
    let result: unknown[] | undefined;
    service.getHistory('x-1').subscribe((h) => (result = h));
    const req = http.expectOne(`${BASE}/exceptions/x-1/history`);
    expect(req.request.method).toBe('GET');
    req.flush({ data: [{ id: 'h-1', eventType: 'raised' }] });
    expect(result?.length).toBe(1);
  });

  it('changes severity carrying the version', () => {
    service
      .changeSeverity('x-1', { severity: 'critical', reason: 'r', version: 'v1' })
      .subscribe();
    const req = http.expectOne(`${BASE}/exceptions/x-1/severity`);
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body.severity).toBe('critical');
    expect(req.request.body.version).toBe('v1');
    req.flush({ data: exception({ severity: 'critical', version: 'v2' }) });
  });

  it('reassigns the owner carrying the version', () => {
    service.reassignOwner('x-1', { ownerUserId: 'u-2', version: 'v1' }).subscribe();
    const req = http.expectOne(`${BASE}/exceptions/x-1/owner`);
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body.ownerUserId).toBe('u-2');
    expect(req.request.body.version).toBe('v1');
    req.flush({ data: exception() });
  });

  it('cancels an exception carrying the version', () => {
    service.cancel('x-1', { reason: 'dropped', version: 'v1' }).subscribe();
    const req = http.expectOne(`${BASE}/exceptions/x-1/cancel`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.reason).toBe('dropped');
    expect(req.request.body.version).toBe('v1');
    req.flush({ data: exception({ status: 'cancelled' }) });
  });

  it('submits a MAP with actions carrying the version', () => {
    service
      .submitMap('x-1', {
        actions: [
          { description: 'fix', ownerUserId: 'u-2', targetDate: '2026-03-01' },
        ],
        version: 'v1',
      })
      .subscribe();
    const req = http.expectOne(`${BASE}/exceptions/x-1/map`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.actions[0].description).toBe('fix');
    expect(req.request.body.version).toBe('v1');
    req.flush({ data: exception({ status: 'map_submitted' }) });
  });

  it('approveMap returns the exception when applied directly (200)', () => {
    let result: ApproveMapResult | undefined;
    service.approveMap('x-1', { version: 'v1' }).subscribe((r) => (result = r));
    const req = http.expectOne(`${BASE}/exceptions/x-1/map/approve`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.version).toBe('v1');
    req.flush({ data: exception({ status: 'map_approved', version: 'v2' }) });
    expect(result?.exception?.status).toBe('map_approved');
    expect(result?.pendingActionId).toBeUndefined();
  });

  it('approveMap surfaces a pendingActionId when maker-checker-gated (202)', () => {
    let result: ApproveMapResult | undefined;
    service.approveMap('x-1', { version: 'v1' }).subscribe((r) => (result = r));
    const req = http.expectOne(`${BASE}/exceptions/x-1/map/approve`);
    req.flush(
      { data: { pendingActionId: 'pa-9' } },
      { status: 202, statusText: 'Accepted' },
    );
    expect(result?.exception).toBeUndefined();
    expect(result?.pendingActionId).toBe('pa-9');
  });

  it('rejects a MAP carrying the version', () => {
    service.rejectMap('x-1', { reason: 'weak', version: 'v1' }).subscribe();
    const req = http.expectOne(`${BASE}/exceptions/x-1/map/reject`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.reason).toBe('weak');
    req.flush({ data: exception({ status: 'map_rejected' }) });
  });

  it('marks the MAP complete carrying the version', () => {
    service.markMapComplete('x-1', { version: 'v1' }).subscribe();
    const req = http.expectOne(`${BASE}/exceptions/x-1/map/mark-complete`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.version).toBe('v1');
    req.flush({ data: exception({ status: 'pending_closure' }) });
  });

  it('returns for evidence carrying the version', () => {
    service
      .returnForEvidence('x-1', { reason: 'more', version: 'v1' })
      .subscribe();
    const req = http.expectOne(`${BASE}/exceptions/x-1/return-for-evidence`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.reason).toBe('more');
    req.flush({ data: exception() });
  });

  it('marks a single action complete carrying the version', () => {
    service
      .markActionComplete('x-1', 'ma-1', { version: 'v1' })
      .subscribe();
    const req = http.expectOne(`${BASE}/exceptions/x-1/map/actions/ma-1`);
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body.version).toBe('v1');
    req.flush({ data: exception() });
  });

  it('closes an exception carrying the version', () => {
    service.close('x-1', { closureNote: 'done', version: 'v1' }).subscribe();
    const req = http.expectOne(`${BASE}/exceptions/x-1/close`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.closureNote).toBe('done');
    expect(req.request.body.version).toBe('v1');
    req.flush({ data: exception({ status: 'closed' }) });
  });

  it('CIA-countersigns carrying the version', () => {
    service.ciaCountersign('x-1', { version: 'v1' }).subscribe();
    const req = http.expectOne(`${BASE}/exceptions/x-1/cia-countersign`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.version).toBe('v1');
    req.flush({ data: exception({ status: 'closed' }) });
  });

  it('lists per-action evidence (no version)', () => {
    let result: unknown[] | undefined;
    service.listActionEvidence('x-1', 'ma-1').subscribe((e) => (result = e));
    const req = http.expectOne(
      `${BASE}/exceptions/x-1/map/actions/ma-1/evidence`,
    );
    expect(req.request.method).toBe('GET');
    req.flush({ data: [{ id: 'e-1' }] });
    expect(result?.length).toBe(1);
  });

  it('uploads per-action evidence as multipart form data (no version)', () => {
    let result: { id: string } | undefined;
    const file = new File(['hello'], 'proof.txt', { type: 'text/plain' });
    service
      .uploadActionEvidence('x-1', 'ma-1', file)
      .subscribe((e) => (result = e));
    const req = http.expectOne(
      `${BASE}/exceptions/x-1/map/actions/ma-1/evidence`,
    );
    expect(req.request.method).toBe('POST');
    expect(req.request.body instanceof FormData).toBe(true);
    expect((req.request.body as FormData).get('file')).toBe(file);
    expect(req.request.withCredentials).toBe(true);
    req.flush({ data: { id: 'e-1', originalFilename: 'proof.txt' } });
    expect(result?.id).toBe('e-1');
  });

  it('downloads evidence as a blob from the audit endpoint', () => {
    let result: Blob | undefined;
    service.downloadEvidence('a-1', 'e-1').subscribe((b) => (result = b));
    const req = http.expectOne(`${BASE}/audits/a-1/evidence/e-1`);
    expect(req.request.method).toBe('GET');
    expect(req.request.responseType).toBe('blob');
    expect(req.request.withCredentials).toBe(true);
    req.flush(new Blob(['data']));
    expect(result instanceof Blob).toBe(true);
  });
});
