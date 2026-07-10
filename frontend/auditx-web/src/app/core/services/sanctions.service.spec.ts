import { TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';

import { SanctionsService } from './sanctions.service';
import { provideTestEnv } from '../../../testing/test-providers';
import {
  SanctionsCase,
  SanctionsGridActionResult,
  SanctionsGridVersion,
} from '../models';

const BASE = '/api/v1';

function sanctionsCase(overrides: Partial<SanctionsCase> = {}): SanctionsCase {
  return {
    id: 'sc-1',
    exceptionId: 'ex-1',
    subjectUserId: null,
    subjectMasked: true,
    status: 'recommendation_drafted',
    category: 'conduct',
    severity: 'high',
    isRecurrence: false,
    recommendation: null,
    gridConsultedVersion: null,
    gridRecommendedRange: null,
    withinGridRange: true,
    deviationReason: null,
    hrOutcomeJson: null,
    dcDecisionJson: null,
    triggeredBy: 'u-1',
    triggeredAt: '2026-06-01T10:00:00Z',
    recommendedBy: null,
    recommendedAt: null,
    submittedAt: null,
    hrOutcomeAt: null,
    dcDecisionAt: null,
    closedBy: null,
    closedAt: null,
    version: 'v1',
    teamMemberUserIds: ['u-1'],
    ...overrides,
  };
}

function gridVersion(
  overrides: Partial<SanctionsGridVersion> = {},
): SanctionsGridVersion {
  return {
    id: 'g-1',
    versionNumber: 1,
    gridDefinitionJson: '{"cells":{}}',
    isActive: true,
    activationReason: null,
    createdByUserId: 'u-1',
    createdAtUtc: '2026-06-01T10:00:00Z',
    activatedBy: null,
    activatedAt: null,
    version: 'gv1',
    ...overrides,
  };
}

describe('SanctionsService', () => {
  let service: SanctionsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideTestEnv()] });
    service = TestBed.inject(SanctionsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lists cases with status/page/pageSize params and unwraps the page', () => {
    let result: { items: unknown[] } | undefined;
    service
      .list('recommendation_submitted', 1, 25)
      .subscribe((page) => (result = page));

    const req = http.expectOne((r) => r.url === `${BASE}/sanctions/cases`);
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('status')).toBe('recommendation_submitted');
    expect(req.request.params.get('page')).toBe('1');
    expect(req.request.params.get('pageSize')).toBe('25');
    req.flush({
      data: {
        items: [sanctionsCase()],
        total: 1,
        page: 1,
        pageSize: 25,
        totalPages: 1,
        hasPrevious: false,
        hasNext: false,
      },
    });
    expect(result?.items.length).toBe(1);
  });

  it('fetches a case by id and unwraps the envelope', () => {
    let result: SanctionsCase | undefined;
    service.getById('sc-1').subscribe((c) => (result = c));
    const req = http.expectOne(`${BASE}/sanctions/cases/sc-1`);
    expect(req.request.method).toBe('GET');
    req.flush({ data: sanctionsCase() });
    expect(result?.id).toBe('sc-1');
  });

  it('lists the DC queue', () => {
    let result: { items: unknown[] } | undefined;
    service.dcQueue(1, 25).subscribe((page) => (result = page));
    const req = http.expectOne((r) => r.url === `${BASE}/sanctions/dc-queue`);
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('page')).toBe('1');
    expect(req.request.params.get('pageSize')).toBe('25');
    req.flush({
      data: {
        items: [sanctionsCase({ status: 'dc_referral' })],
        total: 1,
        page: 1,
        pageSize: 25,
        totalPages: 1,
        hasPrevious: false,
        hasNext: false,
      },
    });
    expect(result?.items.length).toBe(1);
  });

  it('triggers a case from an exception', () => {
    let result: SanctionsCase | undefined;
    service
      .trigger('ex-1', { subjectUserId: 'u-9' })
      .subscribe((c) => (result = c));
    const req = http.expectOne(`${BASE}/exceptions/ex-1/sanctions/trigger`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.subjectUserId).toBe('u-9');
    req.flush({ data: sanctionsCase() });
    expect(result?.exceptionId).toBe('ex-1');
  });

  it('records a recommendation with a deviation reason via PATCH', () => {
    service
      .recordRecommendation('sc-1', {
        recommendation: 'Written warning',
        deviationReason: 'Mitigating circumstances apply here.',
        version: 'v1',
      })
      .subscribe();
    const req = http.expectOne(`${BASE}/sanctions/cases/sc-1/recommendation`);
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body.recommendation).toBe('Written warning');
    expect(req.request.body.deviationReason).toBe(
      'Mitigating circumstances apply here.',
    );
    expect(req.request.body.version).toBe('v1');
    req.flush({ data: sanctionsCase({ recommendation: 'Written warning' }) });
  });

  it('submits a recommendation echoing the version', () => {
    service.submit('sc-1', { version: 'v1' }).subscribe();
    const req = http.expectOne(`${BASE}/sanctions/cases/sc-1/submit`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.version).toBe('v1');
    req.flush({ data: sanctionsCase({ status: 'recommendation_submitted' }) });
  });

  it('downloads the dossier as a blob and exposes the SHA-256 header', () => {
    let response: { headers: { get(name: string): string | null } } | undefined;
    service.generateDossier('sc-1').subscribe((r) => (response = r));

    const req = http.expectOne(`${BASE}/sanctions/cases/sc-1/dossier`);
    expect(req.request.method).toBe('POST');
    expect(req.request.responseType).toBe('blob');
    expect(req.request.withCredentials).toBe(true);

    req.flush(new Blob(['<html></html>'], { type: 'text/html' }), {
      headers: { 'X-Dossier-Sha256': 'deadbeef' },
    });

    expect(response?.headers.get('X-Dossier-Sha256')).toBe('deadbeef');
  });

  it('records an HR outcome', () => {
    service
      .recordHrOutcome('sc-1', {
        outcomeType: 'imposed',
        detail: 'Final written warning issued.',
        version: 'v1',
      })
      .subscribe();
    const req = http.expectOne(`${BASE}/sanctions/cases/sc-1/hr-outcome`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.outcomeType).toBe('imposed');
    req.flush({ data: sanctionsCase({ status: 'hr_outcome_recorded' }) });
  });

  it('refers a case to the DC', () => {
    service
      .referToDc('sc-1', {
        referralReason: 'Severity exceeds HR mandate threshold.',
        version: 'v1',
      })
      .subscribe();
    const req = http.expectOne(`${BASE}/sanctions/cases/sc-1/dc-refer`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.referralReason).toContain('Severity');
    req.flush({ data: sanctionsCase({ status: 'dc_referral' }) });
  });

  it('records a DC decision', () => {
    service
      .recordDcDecision('sc-1', {
        decision: 'uphold',
        rationale: 'The committee agrees with the recommendation.',
        version: 'v1',
      })
      .subscribe();
    const req = http.expectOne(`${BASE}/sanctions/cases/sc-1/dc-decision`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.decision).toBe('uphold');
    req.flush({ data: sanctionsCase({ status: 'dc_decision_recorded' }) });
  });

  it('files an appeal', () => {
    service
      .fileAppeal('sc-1', { basis: 'New evidence has emerged.', version: 'v1' })
      .subscribe();
    const req = http.expectOne(`${BASE}/sanctions/cases/sc-1/appeals`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.basis).toBe('New evidence has emerged.');
    req.flush({
      data: {
        id: 'ap-1',
        sanctionsCaseId: 'sc-1',
        appellantUserId: 'u-2',
        routedToUserId: 'u-3',
        basis: 'New evidence has emerged.',
        status: 'filed',
        decisionJson: null,
        filedAt: '2026-06-02T10:00:00Z',
        decidedAt: null,
        version: 'av1',
      },
    });
  });

  it('decides an appeal', () => {
    service
      .decideAppeal('ap-1', {
        outcome: 'overturn',
        rationale: 'The appeal is upheld on the new evidence.',
        version: 'av1',
      })
      .subscribe();
    const req = http.expectOne(`${BASE}/sanctions/appeals/ap-1/decision`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.outcome).toBe('overturn');
    req.flush({
      data: {
        id: 'ap-1',
        sanctionsCaseId: 'sc-1',
        appellantUserId: 'u-2',
        routedToUserId: 'u-3',
        basis: 'New evidence has emerged.',
        status: 'decided',
        decisionJson: '{"outcome":"overturn"}',
        filedAt: '2026-06-02T10:00:00Z',
        decidedAt: '2026-06-03T10:00:00Z',
        version: 'av2',
      },
    });
  });

  it('closes a case', () => {
    service.close('sc-1', { version: 'v1' }).subscribe();
    const req = http.expectOne(`${BASE}/sanctions/cases/sc-1/close`);
    expect(req.request.method).toBe('POST');
    req.flush({ data: sanctionsCase({ status: 'closed' }) });
  });

  it('fetches the active grid', () => {
    let result: SanctionsGridVersion | null | undefined;
    service.getActiveGrid().subscribe((g) => (result = g));
    const req = http.expectOne(`${BASE}/sanctions/grid`);
    expect(req.request.method).toBe('GET');
    req.flush({ data: gridVersion() });
    expect(result?.versionNumber).toBe(1);
  });

  it('saves a grid draft and returns the grid version on 200', () => {
    let result: SanctionsGridActionResult | undefined;
    service
      .saveGrid({ gridDefinition: '{"cells":{}}' })
      .subscribe((r) => (result = r));
    const req = http.expectOne(`${BASE}/sanctions/grid`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.gridDefinition).toBe('{"cells":{}}');
    req.flush({ data: gridVersion({ isActive: false }) });
    expect(result?.gridVersion?.id).toBe('g-1');
    expect(result?.pendingActionId).toBeUndefined();
  });

  it('saves a grid draft and returns a pendingActionId on 202', () => {
    let result: SanctionsGridActionResult | undefined;
    service
      .saveGrid({ gridDefinition: '{"cells":{}}' })
      .subscribe((r) => (result = r));
    const req = http.expectOne(`${BASE}/sanctions/grid`);
    req.flush(
      { data: { pendingActionId: 'pa-1' } },
      { status: 202, statusText: 'Accepted' },
    );
    expect(result?.pendingActionId).toBe('pa-1');
    expect(result?.gridVersion).toBeUndefined();
  });

  it('activates a grid version and surfaces the 202 pendingActionId', () => {
    let result: SanctionsGridActionResult | undefined;
    service
      .activateGrid('g-1', {
        activationReason: 'Annual review approved by the committee.',
      })
      .subscribe((r) => (result = r));
    const req = http.expectOne(`${BASE}/sanctions/grid/g-1/activate`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.activationReason).toContain('Annual review');
    req.flush(
      { data: { pendingActionId: 'pa-2' } },
      { status: 202, statusText: 'Accepted' },
    );
    expect(result?.pendingActionId).toBe('pa-2');
  });
});
