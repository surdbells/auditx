import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';

import { SanctionsTrackerComponent } from './sanctions-tracker.component';
import { provideTestEnv } from '../../../../../testing/test-providers';
import { SanctionsCaseListItem } from '../../../../core/models';

const BASE = '/api/v1';

function listItem(
  overrides: Partial<SanctionsCaseListItem> = {},
): SanctionsCaseListItem {
  return {
    id: 'sc-1',
    exceptionId: 'ex-1',
    subjectUserId: null,
    subjectMasked: true,
    status: 'recommendation_submitted',
    category: 'conduct',
    severity: 'high',
    isRecurrence: false,
    triggeredAt: '2026-06-01T10:00:00Z',
    ...overrides,
  };
}

function page(items: SanctionsCaseListItem[], total = items.length, pageNum = 1, pageSize = 25) {
  const totalPages = Math.max(1, Math.ceil(total / pageSize));
  return {
    data: {
      items,
      total,
      page: pageNum,
      pageSize,
      totalPages,
      hasPrevious: pageNum > 1,
      hasNext: pageNum < totalPages,
    },
  };
}

describe('SanctionsTrackerComponent', () => {
  let fixture: ComponentFixture<SanctionsTrackerComponent>;
  let component: SanctionsTrackerComponent;
  let http: HttpTestingController;

  function setup(): void {
    TestBed.configureTestingModule({
      imports: [SanctionsTrackerComponent],
      providers: [provideTestEnv(), provideRouter([])],
    });
    fixture = TestBed.createComponent(SanctionsTrackerComponent);
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
  }

  afterEach(() => {
    // The category column lazily loads the sanction_category reference-data (fire-and-forget); drain it before verify.
    http
      .match((r) => r.url.includes('/reference-data/'))
      .forEach((r) => r.flush({ data: [] }));
    http.verify();
  });

  it('loads cases and masks the subject as EMPLOYEE_REDACTED', async () => {
    setup();
    http
      .expectOne((r) => r.url === `${BASE}/sanctions/cases`)
      .flush(page([listItem()]));
    await fixture.whenStable();
    fixture.detectChanges();

    expect(component.cases().length).toBe(1);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('EMPLOYEE_REDACTED');
    // snake_case status is humanised for display.
    expect(text).toContain('Recommendation submitted');
  });

  it('re-fetches with a status query when the filter changes', async () => {
    setup();
    http
      .expectOne((r) => r.url === `${BASE}/sanctions/cases`)
      .flush(page([]));
    await fixture.whenStable();

    component.filters.controls.status.setValue('dc_referral');
    const req = http.expectOne((r) => r.url === `${BASE}/sanctions/cases`);
    expect(req.request.params.get('status')).toBe('dc_referral');
    expect(req.request.params.get('page')).toBe('1');
    expect(req.request.params.get('pageSize')).toBe('25');
    req.flush(page([listItem({ status: 'dc_referral' })]));
  });

  it('omits the status param when "all" is selected', async () => {
    setup();
    const req = http.expectOne((r) => r.url === `${BASE}/sanctions/cases`);
    expect(req.request.params.has('status')).toBe(false);
    req.flush(page([]));
  });

  it('humanises sanctions statuses', () => {
    setup();
    http
      .expectOne((r) => r.url === `${BASE}/sanctions/cases`)
      .flush(page([]));
    expect(component.humanise('recommendation_drafted')).toBe(
      'Recommendation drafted',
    );
    expect(component.humanise('dc_referral')).toBe('Dc referral');
    expect(component.humanise(null)).toBe('—');
  });

  it('shows the masked label even if a subject id leaks through', () => {
    setup();
    http
      .expectOne((r) => r.url === `${BASE}/sanctions/cases`)
      .flush(page([]));
    const masked = component.subjectLabel(
      listItem({ subjectMasked: true, subjectUserId: 'u-secret' }),
    );
    expect(masked).toBe('EMPLOYEE_REDACTED');
  });
});
