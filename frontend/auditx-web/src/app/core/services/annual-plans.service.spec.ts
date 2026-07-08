import { TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';

import { AnnualPlansService } from './annual-plans.service';
import { provideTestEnv } from '../../../testing/test-providers';
import { Plan, PlanItem } from '../models';

const BASE = '/api/v1';

function plan(overrides: Partial<Plan> = {}): Plan {
  return {
    id: 'p-1',
    periodLabel: 'FY2026',
    periodStart: '2026-01-01',
    periodEnd: '2026-12-31',
    status: 'draft',
    submittedAt: null,
    approvedAt: null,
    approvalDecision: null,
    canLaunchAudits: false,
    items: [],
    ...overrides,
  };
}

function planItem(overrides: Partial<PlanItem> = {}): PlanItem {
  return {
    id: 'i-1',
    entityId: 'e-1',
    auditType: 'AML',
    plannedStartDate: '2026-02-01',
    plannedEndDate: '2026-03-01',
    estimatedEffortDays: 10,
    assignedLeadUserId: null,
    linkedAuditId: null,
    status: 'planned',
    orderIndex: 0,
    ...overrides,
  };
}

describe('AnnualPlansService', () => {
  let service: AnnualPlansService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideTestEnv()] });
    service = TestBed.inject(AnnualPlansService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists plans with a status filter and unwraps the page', () => {
    let result: { items: unknown[] } | undefined;
    service
      .list({ status: 'submitted', limit: 20 })
      .subscribe((page) => (result = page));
    const req = http.expectOne((r) => r.url === `${BASE}/annual-plans`);
    expect(req.request.params.get('status')).toBe('submitted');
    req.flush({ data: { items: [], nextCursor: null, hasMore: false } });
    expect(result?.items.length).toBe(0);
  });

  it('gets a plan and unwraps data', () => {
    let result: Plan | undefined;
    service.getById('p-1').subscribe((p) => (result = p));
    http.expectOne(`${BASE}/annual-plans/p-1`).flush({ data: plan() });
    expect(result?.id).toBe('p-1');
  });

  it('fetches execution roll-up', () => {
    service.execution('p-1').subscribe();
    const req = http.expectOne(`${BASE}/annual-plans/p-1/execution`);
    expect(req.request.method).toBe('GET');
    req.flush({
      data: {
        totalItems: 0,
        countsByStatus: {},
        percentComplete: 0,
        behindSchedule: [],
      },
    });
  });

  it('creates a plan', () => {
    service
      .create({
        periodLabel: 'FY2026',
        periodStart: '2026-01-01',
        periodEnd: '2026-12-31',
      })
      .subscribe();
    const req = http.expectOne(`${BASE}/annual-plans`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.periodLabel).toBe('FY2026');
    req.flush({ data: plan() });
  });

  it('adds a plan item', () => {
    let result: PlanItem | undefined;
    service
      .addItem('p-1', {
        entityId: 'e-1',
        auditType: 'AML',
        plannedStartDate: '2026-02-01',
        plannedEndDate: '2026-03-01',
      })
      .subscribe((i) => (result = i));
    const req = http.expectOne(`${BASE}/annual-plans/p-1/items`);
    expect(req.request.method).toBe('POST');
    req.flush({ data: planItem() });
    expect(result?.id).toBe('i-1');
  });

  it('removes a plan item via a void DELETE', () => {
    let done = false;
    service.removeItem('p-1', 'i-1').subscribe(() => (done = true));
    const req = http.expectOne(`${BASE}/annual-plans/p-1/items/i-1`);
    expect(req.request.method).toBe('DELETE');
    req.flush(null, { status: 204, statusText: 'No Content' });
    expect(done).toBe(true);
  });

  it('submits a plan', () => {
    service.submit('p-1').subscribe();
    const req = http.expectOne(`${BASE}/annual-plans/p-1/submit`);
    expect(req.request.method).toBe('POST');
    req.flush({ data: plan({ status: 'submitted' }) });
  });

  it('submits a minor revision', () => {
    service
      .submitRevision('p-1', {
        kind: 'minor',
        itemId: 'i-1',
        newStartDate: '2026-04-01',
        newEndDate: '2026-05-01',
      })
      .subscribe();
    const req = http.expectOne(`${BASE}/annual-plans/p-1/submit-revision`);
    expect(req.request.body.kind).toBe('minor');
    expect(req.request.body.itemId).toBe('i-1');
    req.flush({ data: plan({ status: 'revision_submitted' }) });
  });

  it('posts an AC Chair decision', () => {
    service
      .decision('p-1', { decision: 'approved', comments: 'LGTM' })
      .subscribe();
    const req = http.expectOne(`${BASE}/annual-plans/p-1/decision`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.decision).toBe('approved');
    req.flush({ data: plan({ status: 'approved' }) });
  });

  it('closes a plan via a void POST', () => {
    let done = false;
    service.close('p-1').subscribe(() => (done = true));
    const req = http.expectOne(`${BASE}/annual-plans/p-1/close`);
    expect(req.request.method).toBe('POST');
    req.flush(null, { status: 204, statusText: 'No Content' });
    expect(done).toBe(true);
  });
});
