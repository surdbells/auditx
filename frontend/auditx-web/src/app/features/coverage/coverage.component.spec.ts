import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';

import { CoverageComponent } from './coverage.component';
import { provideTestEnv } from '../../../testing/test-providers';

const BASE = '/api/v1';

describe('CoverageComponent', () => {
  let fixture: ComponentFixture<CoverageComponent>;
  let component: CoverageComponent;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      imports: [CoverageComponent],
      providers: [provideTestEnv()],
    });
    fixture = TestBed.createComponent(CoverageComponent);
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  });

  afterEach(() => http.verify());

  it('runs the not-audited-since report with the months input', () => {
    component.notAuditedForm.setValue({ months: 18, entityType: 'Process' });
    component.runNotAudited();

    const req = http.expectOne(
      (r) => r.url === `${BASE}/coverage-analytics/not-audited-since`,
    );
    expect(req.request.params.get('months')).toBe('18');
    expect(req.request.params.get('entityType')).toBe('Process');
    req.flush({
      data: [
        { id: 'e-1', entityType: 'Process', name: 'Wire', lastAuditedAt: null },
      ],
    });

    expect(component.notAuditedState()).toBe('ready');
    expect(component.notAuditedRows().length).toBe(1);
  });

  it('runs the high-risk-gaps report', () => {
    component.highRiskForm.setValue({ months: 12, entityType: '' });
    component.runHighRisk();

    const req = http.expectOne(
      (r) => r.url === `${BASE}/coverage-analytics/high-risk-gaps`,
    );
    expect(req.request.params.get('months')).toBe('12');
    expect(req.request.params.has('entityType')).toBe(false);
    req.flush({
      data: [
        {
          id: 'e-2',
          entityType: 'System',
          name: 'Core',
          compositeResidualScore: 4.5,
          lastAuditedAt: null,
        },
      ],
    });

    expect(component.highRiskState()).toBe('ready');
    expect(component.highRiskRows().length).toBe(1);
  });

  it('renders the coverage matrix grid', async () => {
    component.runMatrix();

    const req = http.expectOne(
      (r) => r.url === `${BASE}/coverage-analytics/matrix`,
    );
    req.flush({
      data: {
        rows: ['Process', 'System'],
        columns: ['2025', '2026'],
        cells: [
          [2, 0],
          [1, 3],
        ],
      },
    });
    fixture.detectChanges();

    expect(component.matrixState()).toBe('ready');
    expect(component.matrix()?.rows.length).toBe(2);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Process');
    expect(text).toContain('2026');
  });

  it('does not run the not-audited report when months is invalid', () => {
    component.notAuditedForm.setValue({ months: 0, entityType: '' });
    component.runNotAudited();
    http.expectNone(`${BASE}/coverage-analytics/not-audited-since`);
  });
});
