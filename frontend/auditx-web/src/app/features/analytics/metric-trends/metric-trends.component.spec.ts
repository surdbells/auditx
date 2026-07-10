import { ComponentFixture, TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';

import { MetricTrendsComponent } from './metric-trends.component';
import { provideTestEnv } from '../../../../testing/test-providers';
import { MetricComparison } from '../../../core/models';

const BASE = '/api/v1';

function comparison(overrides: Partial<MetricComparison> = {}): MetricComparison {
  return {
    metricKey: 'exceptions.total_open',
    dimension: null,
    period: 'month',
    periods: [
      { label: '2026-05', periodStart: '2026-05-01', periodEnd: '2026-05-31', value: 100 },
      { label: '2026-06', periodStart: '2026-06-01', periodEnd: '2026-06-30', value: 120 },
      { label: '2026-07', periodStart: '2026-07-01', periodEnd: '2026-07-31', value: 150 },
    ],
    current: 150,
    previous: 120,
    delta: 30,
    percentChange: 25,
    forecast: {
      method: 'linear_regression',
      projectedFor: '2026-08-31',
      projectedValue: 172.5,
      slope: 0.8,
    },
    ...overrides,
  };
}

describe('MetricTrendsComponent', () => {
  let fixture: ComponentFixture<MetricTrendsComponent>;
  let component: MetricTrendsComponent;
  let http: HttpTestingController;

  async function setup(first: MetricComparison): Promise<void> {
    TestBed.configureTestingModule({
      imports: [MetricTrendsComponent],
      providers: [provideTestEnv(), provideRouter([])],
    });
    fixture = TestBed.createComponent(MetricTrendsComponent);
    component = fixture.componentInstance;
    http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();

    flushComparison(first);
    await fixture.whenStable();
    fixture.detectChanges();
  }

  function flushComparison(data: MetricComparison): void {
    http.expectOne((r) => r.url === `${BASE}/analytics/metric-comparison`).flush({ data });
  }

  afterEach(() => http.verify());

  it('renders the comparison summary, forecast and chart series', async () => {
    await setup(comparison());
    expect(component.comparison()?.current).toBe(150);
    expect(component.deltaDirection()).toBe('up');
    expect(component.chartData().length).toBe(3);

    const text = (fixture.nativeElement as HTMLElement).textContent ?? '';
    expect(text).toContain('150'); // current
    expect(text).toContain('25%'); // percent change
    expect(text).toContain('172.5'); // forecast projected value
  });

  it('maps a gap period to NaN so the chart skips it', async () => {
    await setup(
      comparison({
        periods: [
          { label: '2026-05', periodStart: '2026-05-01', periodEnd: '2026-05-31', value: 80 },
          { label: '2026-06', periodStart: '2026-06-01', periodEnd: '2026-06-30', value: null },
          { label: '2026-07', periodStart: '2026-07-01', periodEnd: '2026-07-31', value: 90 },
        ],
        current: 90,
        previous: 80,
        delta: 10,
        percentChange: 12.5,
      }),
    );
    const midpoint = component.chartData()[1];
    expect(Number.isNaN(midpoint.value)).toBe(true);
  });

  it('refetches when the comparison period changes', async () => {
    await setup(comparison());

    component.selectPeriod('quarter');
    const req = http.expectOne((r) => r.url === `${BASE}/analytics/metric-comparison`);
    expect(req.request.params.get('period')).toBe('quarter');
    req.flush({ data: comparison({ period: 'quarter' }) });

    expect(component.period()).toBe('quarter');
  });

  it('shows the empty state when no period has a value', async () => {
    await setup(
      comparison({
        periods: [
          { label: '2026-06', periodStart: '2026-06-01', periodEnd: '2026-06-30', value: null },
          { label: '2026-07', periodStart: '2026-07-01', periodEnd: '2026-07-31', value: null },
        ],
        current: null,
        previous: null,
        delta: null,
        percentChange: null,
        forecast: null,
      }),
    );
    expect(component.hasSeries()).toBe(false);
  });
});
