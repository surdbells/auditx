import { TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';

import { CoverageService } from './coverage.service';
import { provideTestEnv } from '../../../testing/test-providers';

const BASE = '/api/v1';

describe('CoverageService', () => {
  let service: CoverageService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideTestEnv()] });
    service = TestBed.inject(CoverageService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('requests not-audited-since with months and optional entity type', () => {
    service.notAuditedSince(12, 'Process').subscribe();
    const req = http.expectOne(
      (r) => r.url === `${BASE}/coverage-analytics/not-audited-since`,
    );
    expect(req.request.params.get('months')).toBe('12');
    expect(req.request.params.get('entityType')).toBe('Process');
    req.flush({ data: [] });
  });

  it('requests high-risk-gaps with months', () => {
    let result: unknown[] | undefined;
    service.highRiskGaps(24).subscribe((rows) => (result = rows));
    const req = http.expectOne(
      (r) => r.url === `${BASE}/coverage-analytics/high-risk-gaps`,
    );
    expect(req.request.params.get('months')).toBe('24');
    req.flush({
      data: [
        {
          id: 'e-1',
          entityType: 'Process',
          name: 'Wire',
          compositeResidualScore: 4.2,
          lastAuditedAt: null,
        },
      ],
    });
    expect(result?.length).toBe(1);
  });

  it('requests the coverage matrix with a window', () => {
    let result: { rows: string[] } | undefined;
    service.matrix('36m').subscribe((m) => (result = m));
    const req = http.expectOne(
      (r) => r.url === `${BASE}/coverage-analytics/matrix`,
    );
    expect(req.request.params.get('window')).toBe('36m');
    req.flush({ data: { rows: ['Process'], columns: ['2026'], cells: [[2]] } });
    expect(result?.rows.length).toBe(1);
  });
});
