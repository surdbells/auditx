import { TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';

import { ReportsService } from './reports.service';
import { provideTestEnv } from '../../../testing/test-providers';
import {
  Report,
  ReportListItem,
  ReportTemplate,
} from '../models';

const BASE = '/api/v1';

function report(overrides: Partial<Report> = {}): Report {
  return {
    id: 'r-1',
    auditId: 'au-1',
    kind: 'audit_engagement',
    versionNumber: 1,
    status: 'completed',
    sha256Hash: 'deadbeef',
    templateId: 't-1',
    templateVersionSnapshot: 1,
    requestedFormats: ['html'],
    producedArtefacts: [
      {
        format: 'html',
        contentType: 'text/html',
        sizeBytes: 1024,
        sha256: 'deadbeef',
      },
    ],
    failureReason: null,
    generatedBy: 'u-1',
    requestedAt: '2026-06-01T10:00:00Z',
    completedAt: '2026-06-01T10:01:00Z',
    version: 'v1',
    ...overrides,
  };
}

function listItem(overrides: Partial<ReportListItem> = {}): ReportListItem {
  return {
    id: 'r-1',
    auditId: 'au-1',
    kind: 'audit_engagement',
    versionNumber: 1,
    status: 'completed',
    sha256Hash: 'deadbeef',
    producedFormats: ['html'],
    generatedBy: 'u-1',
    requestedAt: '2026-06-01T10:00:00Z',
    completedAt: '2026-06-01T10:01:00Z',
    ...overrides,
  };
}

function template(overrides: Partial<ReportTemplate> = {}): ReportTemplate {
  return {
    id: 'rt-1',
    name: 'Standard report',
    versionNumber: 1,
    templateDefinitionJson: '{"sections":[]}',
    isActive: true,
    activationReason: null,
    createdByUserId: 'u-1',
    createdAtUtc: '2026-06-01T10:00:00Z',
    activatedBy: null,
    activatedAt: null,
    version: 'tv1',
    ...overrides,
  };
}

describe('ReportsService', () => {
  let service: ReportsService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideTestEnv()] });
    service = TestBed.inject(ReportsService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('generates a report and returns the 202 body', () => {
    let result: { reportId: string; status: string } | undefined;
    service
      .generate('au-1', { docx: true })
      .subscribe((r) => (result = r));

    const req = http.expectOne(`${BASE}/audits/au-1/reports`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.docx).toBe(true);
    req.flush(
      { data: { reportId: 'r-9', status: 'pending' } },
      { status: 202, statusText: 'Accepted' },
    );
    expect(result?.reportId).toBe('r-9');
    expect(result?.status).toBe('pending');
  });

  it('lists the report versions for an audit as a cursor page', () => {
    let result: { items: ReportListItem[]; hasMore: boolean } | undefined;
    service.listForAudit('au-1').subscribe((page) => (result = page));
    const req = http.expectOne((r) => r.url === `${BASE}/audits/au-1/reports`);
    expect(req.request.method).toBe('GET');
    req.flush({ data: { items: [listItem()], nextCursor: null, hasMore: false } });
    expect(result?.items.length).toBe(1);
    expect(result?.items[0].versionNumber).toBe(1);
    expect(result?.hasMore).toBe(false);
  });

  it('forwards cursor + limit when paging the audit report list', () => {
    service.listForAudit('au-1', 'cur-1', 20).subscribe();
    const req = http.expectOne((r) => r.url === `${BASE}/audits/au-1/reports`);
    expect(req.request.params.get('cursor')).toBe('cur-1');
    expect(req.request.params.get('limit')).toBe('20');
    req.flush({ data: { items: [], nextCursor: null, hasMore: false } });
  });

  it('fetches a single report (status surface) and unwraps the envelope', () => {
    let result: Report | undefined;
    service.getById('r-1').subscribe((r) => (result = r));
    const req = http.expectOne(`${BASE}/reports/r-1`);
    expect(req.request.method).toBe('GET');
    req.flush({ data: report({ status: 'running' }) });
    expect(result?.status).toBe('running');
  });

  it('downloads an artefact as a blob with the format param', () => {
    let response: { body: Blob | null } | undefined;
    service.download('r-1', 'docx').subscribe((r) => (response = r));

    const req = http.expectOne(
      (r) => r.url === `${BASE}/reports/r-1/download`,
    );
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('format')).toBe('docx');
    expect(req.request.responseType).toBe('blob');
    expect(req.request.withCredentials).toBe(true);

    req.flush(new Blob(['<html></html>'], { type: 'text/html' }));
    expect(response?.body).toBeInstanceOf(Blob);
  });

  it('surfaces the 500 integrity error from a download', () => {
    let errorStatus: number | undefined;
    service.download('r-1', 'html').subscribe({
      error: (err: { status: number }) => (errorStatus = err.status),
    });
    const req = http.expectOne(
      (r) => r.url === `${BASE}/reports/r-1/download`,
    );
    req.flush(new Blob(['error']), {
      status: 500,
      statusText: 'Internal Server Error',
    });
    expect(errorStatus).toBe(500);
  });

  it('verifies the artefact hash', () => {
    let match: boolean | undefined;
    service
      .verifyHash('r-1')
      .subscribe((v) => (match = v.match));
    const req = http.expectOne(`${BASE}/reports/r-1/verify-hash`);
    expect(req.request.method).toBe('GET');
    req.flush({
      data: { storedHash: 'abc', recomputedHash: 'abc', match: true },
    });
    expect(match).toBe(true);
  });

  it('distributes a report to users and free-text emails', () => {
    let count: number | undefined;
    service
      .distribute('r-1', {
        recipientUserIds: ['u-1'],
        recipientEmailAddresses: ['x@bank.test'],
      })
      .subscribe((r) => (count = r.recipientCount));
    const req = http.expectOne(`${BASE}/reports/r-1/distribute`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.recipientUserIds).toEqual(['u-1']);
    expect(req.request.body.recipientEmailAddresses).toEqual(['x@bank.test']);
    req.flush({ data: { recipientCount: 2 } });
    expect(count).toBe(2);
  });

  it('lists distributions with cursor + limit and unwraps the page', () => {
    let result: { items: unknown[] } | undefined;
    service
      .distributions('r-1', 'cur-1', 20)
      .subscribe((page) => (result = page));
    const req = http.expectOne(
      (r) => r.url === `${BASE}/reports/r-1/distributions`,
    );
    expect(req.request.method).toBe('GET');
    expect(req.request.params.get('cursor')).toBe('cur-1');
    expect(req.request.params.get('limit')).toBe('20');
    req.flush({
      data: {
        items: [
          {
            id: 'd-1',
            reportId: 'r-1',
            reportVersionNumber: 1,
            recipientUserId: 'u-1',
            recipientEmail: null,
            dispatchedAt: '2026-06-01T10:05:00Z',
            dispatchedBy: 'u-2',
            outcome: 'sent',
          },
        ],
        nextCursor: null,
        hasMore: false,
      },
    });
    expect(result?.items.length).toBe(1);
  });

  it('lists report templates', () => {
    let result: ReportTemplate[] | undefined;
    service.listTemplates().subscribe((r) => (result = r));
    const req = http.expectOne(`${BASE}/report-templates`);
    expect(req.request.method).toBe('GET');
    req.flush({ data: [template()] });
    expect(result?.length).toBe(1);
  });

  it('creates a report template', () => {
    let result: ReportTemplate | undefined;
    service
      .createTemplate({
        name: 'New template',
        templateDefinitionJson: '{"sections":[]}',
      })
      .subscribe((r) => (result = r));
    const req = http.expectOne(`${BASE}/report-templates`);
    expect(req.request.method).toBe('POST');
    expect(req.request.body.name).toBe('New template');
    req.flush({ data: template({ name: 'New template', isActive: false }) });
    expect(result?.name).toBe('New template');
  });

  it('activates a report template with a reason via PATCH', () => {
    service
      .activateTemplate('rt-1', {
        reason: 'Approved for the new reporting cycle.',
      })
      .subscribe();
    const req = http.expectOne(`${BASE}/report-templates/rt-1/activate`);
    expect(req.request.method).toBe('PATCH');
    expect(req.request.body.reason).toContain('Approved');
    req.flush({ data: template({ activationReason: 'Approved' }) });
  });
});
