import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';

import { AcDashboardComponent } from './ac-dashboard.component';
import { provideTestEnv } from '../../../../testing/test-providers';
import { AcDashboard } from '../../../core/models';

const BASE = '/api/v1';

function dashboard(overrides: Partial<AcDashboard> = {}): AcDashboard {
  return {
    totalPlans: 2,
    planItemsTotal: 20,
    planItemsCompleted: 15,
    planCompletionPercent: 75,
    openExceptionTotal: 9,
    averageClosureDays: 10,
    exceptionsBySeverity: [{ severity: 'high', count: 3 }],
    materialFindings: [],
    sanctionsTotalCases: 4,
    sanctionsGridAdherencePercent: 80,
    sanctionsAppealRatePercent: 25,
    sanctionsByBusinessUnit: [
      {
        businessUnit: 'Retail',
        caseCount: 4,
        withinGridCount: 3,
        gridAdherencePercent: 75,
        deviationCount: 1,
        appealCount: 1,
        appealRatePercent: 25,
      },
    ],
    recurrenceClusters: [],
    ...overrides,
  };
}

describe('AcDashboardComponent', () => {
  let fixture: ComponentFixture<AcDashboardComponent>;
  let component: AcDashboardComponent;
  let http: HttpTestingController;

  async function setup(d: AcDashboard): Promise<void> {
    TestBed.configureTestingModule({
      imports: [AcDashboardComponent],
      providers: [provideTestEnv()],
    });
    fixture = TestBed.createComponent(AcDashboardComponent);
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();

    await fixture.whenStable();
    http.expectOne(`${BASE}/ac-dashboard`).flush({ data: d });
    await fixture.whenStable();
    fixture.detectChanges();
  }

  afterEach(() => http.verify());

  it('renders the read-only aggregates', async () => {
    await setup(dashboard());
    expect(component.state()).toBe('ready');
    expect(component.dashboard()?.openExceptionTotal).toBe(9);
    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('Sanctions consistency');
    expect(text).toContain('Retail');
  });

  it('surfaces an error state when the dashboard call fails', async () => {
    TestBed.configureTestingModule({
      imports: [AcDashboardComponent],
      providers: [provideTestEnv()],
    });
    fixture = TestBed.createComponent(AcDashboardComponent);
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();

    await fixture.whenStable();
    http
      .expectOne(`${BASE}/ac-dashboard`)
      .flush('boom', { status: 500, statusText: 'Server Error' });
    await fixture.whenStable();
    expect(component.state()).toBe('error');
  });
});