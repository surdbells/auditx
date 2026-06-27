import { TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';

import { AuditTrailService } from './audit-trail.service';
import { provideTestEnv } from '../../../testing/test-providers';
import { AuditTrailEntry, FlaggedEvidence } from '../models';

const BASE = '/api/v1';

function entry(overrides: Partial<AuditTrailEntry> = {}): AuditTrailEntry {
  return {
    id: 'a-1',
    eventType: 'audit.created',
    targetObjectType: 'Audit',
    targetObjectId: 't-1',
    actorUserId: 'u-1',
    actorType: 'user',
    actorSystemLabel: null,
    occurredAtUtc: '2026-06-01T10:00:00Z',
    originatingTimezone: 'Africa/Lagos',
    beforeStateJson: null,
    afterStateJson: '{"status":"draft"}',
    requestContextJson: null,
    eventPayloadJson: null,
    ...overrides,
  };
}

function flagged(overrides: Partial<FlaggedEvidence> = {}): FlaggedEvidence {
  return {
    id: 'e-1',
    auditId: 'au-1',
    originalFilename: 'evidence.pdf',
    mimeType: 'application/pdf',
    sizeBytes: 1024,
    sha256Hash: 'abc123',
    uploadedAt: '2026-06-01T10:00:00Z',
    uploadedBy: 'u-1',
    ...overrides,
  };
}

describe('AuditTrailService', () => {
  let service: AuditTrailService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideTestEnv()] });
    service = TestBed.inject(AuditTrailService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('queries with snake_case params and unwraps the page', () => {
    let result: { items: unknown[] } | undefined;
    service
      .query(
        {
          actorUserId: 'u-1',
          eventType: 'audit.created',
          targetObjectType: 'Audit',
          targetObjectId: 't-1',
          dateFrom: '2026-01-01T00:00:00Z',
          dateTo: '2026-12-31T23:59:59Z',
        },
        'cur-1',
        50,
      )
      .subscribe((page) => (result = page));

    const req = http.expectOne((r) => r.url === `${BASE}/audit-trail`);
    expect(req.request.params.get('actor_user_id')).toBe('u-1');
    expect(req.request.params.get('event_type')).toBe('audit.created');
    expect(req.request.params.get('target_object_type')).toBe('Audit');
    expect(req.request.params.get('target_object_id')).toBe('t-1');
    expect(req.request.params.get('date_from')).toBe('2026-01-01T00:00:00Z');
    expect(req.request.params.get('date_to')).toBe('2026-12-31T23:59:59Z');
    expect(req.request.params.get('cursor')).toBe('cur-1');
    expect(req.request.params.get('limit')).toBe('50');
    req.flush({
      data: { items: [entry()], nextCursor: null, hasMore: false },
    });
    expect(result?.items.length).toBe(1);
  });

  it('fetches paginated object history', () => {
    let result: { items: unknown[] } | undefined;
    service.objectHistory('Audit', 't-1').subscribe((r) => (result = r));
    const req = http.expectOne(`${BASE}/audit-trail/object/Audit/t-1`);
    expect(req.request.method).toBe('GET');
    req.flush({ data: { items: [entry()], nextCursor: null, hasMore: false } });
    expect(result?.items.length).toBe(1);
  });

  it('downloads the CSV export as a blob and exposes the SHA-256 header', () => {
    let response: { headers: { get(name: string): string | null } } | undefined;
    service
      .exportCsv({ eventType: 'audit.created' })
      .subscribe((r) => (response = r));

    const req = http.expectOne(
      (r) => r.url === `${BASE}/audit-trail/export`,
    );
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('event_type')).toBe('audit.created');
    expect(req.request.responseType).toBe('blob');
    expect(req.request.withCredentials).toBe(true);

    req.flush(new Blob(['id,event\n'], { type: 'text/csv' }), {
      headers: { 'X-Content-SHA256': 'deadbeef', 'X-Row-Count': '1' },
    });

    expect(response?.headers.get('X-Content-SHA256')).toBe('deadbeef');
    expect(response?.headers.get('X-Row-Count')).toBe('1');
  });

  it('unflags evidence via a void POST carrying the resolution', () => {
    let done = false;
    service
      .unflagEvidence('au-1', 'e-1', 'false positive')
      .subscribe(() => (done = true));
    const req = http.expectOne(
      `${BASE}/audits/au-1/evidence/e-1/unflag`,
    );
    expect(req.request.method).toBe('POST');
    expect(req.request.body.resolution).toBe('false positive');
    req.flush(null, { status: 204, statusText: 'No Content' });
    expect(done).toBe(true);
  });

  it('lists flagged evidence and unwraps the array', () => {
    let result: FlaggedEvidence[] | undefined;
    service.listFlagged().subscribe((r) => (result = r));
    const req = http.expectOne(`${BASE}/evidence/flagged`);
    expect(req.request.method).toBe('GET');
    req.flush({ data: [flagged()] });
    expect(result?.length).toBe(1);
    expect(result?.[0].originalFilename).toBe('evidence.pdf');
  });
});
