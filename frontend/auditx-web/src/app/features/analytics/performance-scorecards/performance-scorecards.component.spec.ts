import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';

import { PerformanceScorecardsComponent } from './performance-scorecards.component';
import { provideTestEnv } from '../../../../testing/test-providers';
import { PerformanceScorecard } from '../../../core/models';

const BASE = '/api/v1';

function scorecard(
  overrides: Partial<PerformanceScorecard> = {},
): PerformanceScorecard {
  return {
    auditLeadUserId: 'lead-1',
    auditsLed: 4,
    auditsCompleted: 3,
    averageCycleDays: 21.5,
    exceptionsRaised: 9,
    exceptionsClosed: 7,
    averageExceptionClosureDays: null,
    ...overrides,
  };
}

describe('PerformanceScorecardsComponent', () => {
  let fixture: ComponentFixture<PerformanceScorecardsComponent>;
  let component: PerformanceScorecardsComponent;
  let http: HttpTestingController;

  async function setup(rows: PerformanceScorecard[]): Promise<void> {
    TestBed.configureTestingModule({
      imports: [PerformanceScorecardsComponent],
      providers: [provideTestEnv(), provideRouter([])],
    });
    fixture = TestBed.createComponent(PerformanceScorecardsComponent);
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();

    http
      .expectOne(`${BASE}/analytics/performance-scorecards`)
      .flush({ data: rows });
    await fixture.whenStable();
    fixture.detectChanges();

    // Rendering a lead name lazily loads the user directory.
    flushUserDirectory();
  }

  /** Flushes the (at most one) lazy user-directory GET the lead label triggers. */
  function flushUserDirectory(): void {
    for (const req of http.match((r) => r.url === `${BASE}/users/directory`)) {
      req.flush({ data: { items: [], nextCursor: null, hasMore: false } });
    }
  }

  afterEach(() => {
    flushUserDirectory();
    http.verify();
  });

  it('renders scorecard rows and formats nullable averages', async () => {
    await setup([scorecard()]);
    expect(component.scorecards().length).toBe(1);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('lead-1');
    expect(text).toContain('21.5'); // average cycle days
    expect(text).toContain('—'); // null closure days renders a dash
  });

  it('shows the empty state when there are no scorecards', async () => {
    await setup([]);
    expect(component.isEmpty()).toBe(true);
  });
});
