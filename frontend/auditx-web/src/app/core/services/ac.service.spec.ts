import { TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';

import { AcService } from './ac.service';
import { provideTestEnv } from '../../../testing/test-providers';
import { AcActionItem, AcPack, AcPackListItem } from '../models';

const BASE = '/api/v1';

function pack(overrides: Partial<AcPack> = {}): AcPack {
  return {
    id: 'p-1',
    versionNumber: 1,
    status: 'pending_review',
    periodStart: '2026-01-01',
    periodEnd: '2026-03-31',
    acMeetingLabel: 'Q1 AC',
    sha256Hash: 'deadbeef',
    ciaSupplementaryText: null,
    requestedFormats: ['html'],
    producedArtefacts: [
      {
        format: 'html',
        contentType: 'text/html',
        sizeBytes: 2048,
        sha256: 'deadbeef',
      },
    ],
    failureReason: null,
    generatedBy: 'u-1',
    requestedAt: '2026-04-01T10:00:00Z',
    completedAt: '2026-04-01T10:01:00Z',
    approvedBy: null,
    approvedAt: null,
    version: 'v1',
    ...overrides,
  };
}

function listItem(overrides: Partial<AcPackListItem> = {}): AcPackListItem {
  return {
    id: 'p-1',
    versionNumber: 1,
    status: 'distributed',
    periodStart: '2026-01-01',
    periodEnd: '2026-03-31',
    acMeetingLabel: 'Q1 AC',
    sha256Hash: 'deadbeef',
    producedFormats: ['html'],
    generatedBy: 'u-1',
    requestedAt: '2026-04-01T10:00:00Z',
    completedAt: '2026-04-01T10:01:00Z',
    ...overrides,
  };
}

function actionItem(overrides: Partial<AcActionItem> = {}): AcActionItem {
  return {
    id: 'a-1',
    title: 'Follow up on AML gap',
    description: null,
    status: 'open',
    assignedToUserId: null,
    dueDate: null,
    closureResponse: null,
    createdByUserId: 'u-1',
    closedAt: null,
    closedByUserId: null,
    acknowledgedAt: null,
    acknowledgedByUserId: null,
    version: 'av1',
    ...overrides,
  };
}

describe('AcService', () => {
  let service: AcService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideTestEnv()] });
    service = TestBed.inject(AcService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('generates a pack and returns the 202 body', () => {
    let result: { acPackId: string; status: string } | undefined;
    service
      .generatePack({
        periodStart: '2026-01-01',
        periodEnd: '2026-03-31',
        acMeetingLabel: 'Q1 AC',
        docx: true,
      })
      .subscribe((r) => (result = r));

    const req = http.expectOne(`${BASE}/ac-packs/generate`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.docx).toBe(true);
    expect(req.request.body.acMeetingLabel).toBe('Q1 AC');
    req.flush(
      { data: { acPackId: 'p-9', status: 'pending' } },
      { status: 202, statusText: 'Accepted' },
    );
    expect(result?.acPackId).toBe('p-9');
    expect(result?.status).toBe('pending');
  });

  it('lists packs with status + cursor + limit and unwraps the page', () => {
    let result: { items: unknown[] } | undefined;
    service
      .listPacks({ status: 'distributed', cursor: 'cur-1', limit: 25 })
      .subscribe((page) => (result = page));

    const req = http.expectOne((r) => r.url === `${BASE}/ac-packs`);
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('status')).toBe('distributed');
    expect(req.request.params.get('cursor')).toBe('cur-1');
    expect(req.request.params.get('limit')).toBe('25');
    req.flush({
      data: { items: [listItem()], nextCursor: null, hasMore: false },
    });
    expect(result?.items.length).toBe(1);
  });

  it('fetches a single pack (status surface) and unwraps the envelope', () => {
    let result: AcPack | undefined;
    service.getPack('p-1').subscribe((r) => (result = r));
    const req = http.expectOne(`${BASE}/ac-packs/p-1`);
    expect(req.request.method).toBe('GET');
    req.flush({ data: pack({ status: 'generating' }) });
    expect(result?.status).toBe('generating');
  });

  it('fetches the pack analytics snapshot', () => {
    let total: number | undefined;
    service.getPackAnalytics('p-1').subscribe((a) => (total = a.openExceptionTotal));
    const req = http.expectOne(`${BASE}/ac-packs/p-1/analytics`);
    expect(req.request.method).toBe('GET');
    req.flush({
      data: {
        versionNumber: 1,
        periodStart: '2026-01-01',
        periodEnd: '2026-03-31',
        totalPlans: 1,
        planItemsTotal: 10,
        planItemsCompleted: 7,
        planCompletionPercent: 70,
        openExceptionTotal: 4,
        averageClosureDays: 12.5,
        exceptionsBySeverity: [{ severity: 'critical', count: 1 }],
        materialFindings: [],
        sanctionsTotalCases: 0,
        sanctionsGridAdherencePercent: 0,
        sanctionsAppealRatePercent: 0,
        sanctionsByBusinessUnit: [],
        recurrenceClusters: [],
        generatedAtUtc: '2026-04-01T10:01:00Z',
      },
    });
    expect(total).toBe(4);
  });

  it('downloads a pack artefact as a blob with the format param', () => {
    let response: { body: Blob | null } | undefined;
    service.downloadPack('p-1', 'docx').subscribe((r) => (response = r));

    const req = http.expectOne((r) => r.url === `${BASE}/ac-packs/p-1/download`);
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('format')).toBe('docx');
    expect(req.request.responseType).toBe('blob');
    expect(req.request.withCredentials).toBe(true);

    req.flush(new Blob(['<html></html>'], { type: 'text/html' }));
    expect(response?.body).toBeInstanceOf(Blob);
  });

  it('patches the CIA supplementary text', () => {
    let result: AcPack | undefined;
    service
      .updateCiaText('p-1', { supplementaryText: 'Board narrative.' })
      .subscribe((r) => (result = r));
    const req = http.expectOne(`${BASE}/ac-packs/p-1/cia-text`);
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body.supplementaryText).toBe('Board narrative.');
    req.flush({ data: pack({ ciaSupplementaryText: 'Board narrative.' }) });
    expect(result?.ciaSupplementaryText).toBe('Board narrative.');
  });

  it('approves a pack', () => {
    let result: AcPack | undefined;
    service
      .approvePack('p-1', { supplementaryText: 'Final note.' })
      .subscribe((r) => (result = r));
    const req = http.expectOne(`${BASE}/ac-packs/p-1/approve`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.supplementaryText).toBe('Final note.');
    req.flush({ data: pack({ status: 'approved', approvedBy: 'u-1' }) });
    expect(result?.status).toBe('approved');
  });

  it('distributes a pack and returns the recipient count', () => {
    let count: number | undefined;
    service.distributePack('p-1').subscribe((r) => (count = r.recipientCount));
    const req = http.expectOne(`${BASE}/ac-packs/p-1/distribute`);
    expect(req.request.method).toBe('POST');
    req.flush({ data: { recipientCount: 5 } });
    expect(count).toBe(5);
  });

  it('lists pack distributions with cursor + limit', () => {
    let result: { items: unknown[] } | undefined;
    service
      .packDistributions('p-1', 'cur-1', 20)
      .subscribe((page) => (result = page));
    const req = http.expectOne(
      (r) => r.url === `${BASE}/ac-packs/p-1/distributions`,
    );
    expect(req.request.params.get('cursor')).toBe('cur-1');
    expect(req.request.params.get('limit')).toBe('20');
    req.flush({
      data: {
        items: [
          {
            id: 'd-1',
            acPackId: 'p-1',
            acPackVersionNumber: 1,
            recipientUserId: 'u-2',
            dispatchedAt: '2026-04-02T10:00:00Z',
            dispatchedBy: 'u-1',
            outcome: 'sent',
          },
        ],
        nextCursor: null,
        hasMore: false,
      },
    });
    expect(result?.items.length).toBe(1);
  });

  it('fetches the read-only AC dashboard', () => {
    let openTotal: number | undefined;
    service.getDashboard().subscribe((d) => (openTotal = d.openExceptionTotal));
    const req = http.expectOne(`${BASE}/ac-dashboard`);
    expect(req.request.method).toBe('GET');
    req.flush({
      data: {
        totalPlans: 2,
        planItemsTotal: 20,
        planItemsCompleted: 15,
        planCompletionPercent: 75,
        openExceptionTotal: 9,
        averageClosureDays: 10,
        exceptionsBySeverity: [],
        materialFindings: [],
        sanctionsTotalCases: 0,
        sanctionsGridAdherencePercent: 0,
        sanctionsAppealRatePercent: 0,
        sanctionsByBusinessUnit: [],
        recurrenceClusters: [],
      },
    });
    expect(openTotal).toBe(9);
  });

  it('creates an action item', () => {
    let result: AcActionItem | undefined;
    service
      .createActionItem({ title: 'New item', dueDate: '2026-05-01' })
      .subscribe((r) => (result = r));
    const req = http.expectOne(`${BASE}/ac-action-items`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.title).toBe('New item');
    req.flush(
      { data: actionItem({ title: 'New item', dueDate: '2026-05-01' }) },
      { status: 201, statusText: 'Created' },
    );
    expect(result?.title).toBe('New item');
  });

  it('lists action items by status', () => {
    let result: { items: unknown[] } | undefined;
    service.listActionItems('open').subscribe((page) => (result = page));
    const req = http.expectOne((r) => r.url === `${BASE}/ac-action-items`);
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('status')).toBe('open');
    req.flush({ data: { items: [actionItem()], nextCursor: null, hasMore: false } });
    expect(result?.items.length).toBe(1);
  });

  it('closes an action item with a closure response (PATCH)', () => {
    let result: AcActionItem | undefined;
    service
      .updateActionItem('a-1', { closureResponse: 'Remediated and verified.' })
      .subscribe((r) => (result = r));
    const req = http.expectOne(`${BASE}/ac-action-items/a-1`);
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body.closureResponse).toContain('Remediated');
    req.flush({
      data: actionItem({ status: 'closed', closureResponse: 'Remediated and verified.' }),
    });
    expect(result?.status).toBe('closed');
  });

  it('acknowledges an action item closure (chair)', () => {
    let result: AcActionItem | undefined;
    service
      .acknowledgeActionItemClosure('a-1')
      .subscribe((r) => (result = r));
    const req = http.expectOne(`${BASE}/ac-action-items/a-1/acknowledge-closure`);
    expect(req.request.method).toBe('POST');
    req.flush({ data: actionItem({ status: 'acknowledged' }) });
    expect(result?.status).toBe('acknowledged');
  });

  it('adds a comment against a target', () => {
    service
      .addComment({ targetType: 'plan', targetId: 't-1', comment: 'Looks good.' })
      .subscribe();
    const req = http.expectOne(`${BASE}/ac-comments`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.targetType).toBe('plan');
    expect(req.request.body.targetId).toBe('t-1');
    req.flush(
      {
        data: {
          id: 'c-1',
          targetType: 'plan',
          targetId: 't-1',
          commentText: 'Looks good.',
          authorUserId: 'u-1',
          commentedAt: '2026-04-02T10:00:00Z',
        },
      },
      { status: 201, statusText: 'Created' },
    );
  });

  it('lists comments by target type + id', () => {
    let result: unknown[] | undefined;
    service.listComments('pack', 't-1').subscribe((r) => (result = r));
    const req = http.expectOne((r) => r.url === `${BASE}/ac-comments`);
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('targetType')).toBe('pack');
    expect(req.request.params.get('targetId')).toBe('t-1');
    req.flush({ data: [] });
    expect(result?.length).toBe(0);
  });

  it('restricts a finding visibility with an allow-list', () => {
    service
      .restrictFindingVisibility('exception', 'f-1', {
        allowedUserIds: ['u-2', 'u-3'],
        reason: 'Sensitive.',
      })
      .subscribe();
    const req = http.expectOne(
      `${BASE}/findings/exception/f-1/restrict-visibility`,
    );
    expect(req.request.method).toBe('POST');
    expect(req.request.body.allowedUserIds).toEqual(['u-2', 'u-3']);
    req.flush({
      data: {
        id: 'fv-1',
        findingType: 'exception',
        findingId: 'f-1',
        allowedUserIds: ['u-2', 'u-3'],
        reason: 'Sensitive.',
        restrictedByUserId: 'u-1',
        restrictedAt: '2026-04-02T10:00:00Z',
      },
    });
  });
});