import { ComponentFixture, TestBed } from '@angular/core/testing';

import { LineChartComponent } from './line-chart.component';
import { PointDatum } from '../chart-types';

describe('LineChartComponent', () => {
  let fixture: ComponentFixture<LineChartComponent>;

  function setup(
    data: PointDatum[],
    inputs: Record<string, unknown> = {},
  ): HTMLElement {
    TestBed.configureTestingModule({ imports: [LineChartComponent] });
    fixture = TestBed.createComponent(LineChartComponent);
    fixture.componentRef.setInput('data', data);
    for (const [key, value] of Object.entries(inputs)) {
      fixture.componentRef.setInput(key, value);
    }
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('draws a polyline through the series', () => {
    const host = setup([
      { value: 1, label: 'Jan' },
      { value: 4, label: 'Feb' },
      { value: 2, label: 'Mar' },
    ]);
    const poly = host.querySelector('polyline');
    expect(poly).toBeTruthy();
    // Three "x,y" coordinate pairs.
    expect((poly?.getAttribute('points') ?? '').trim().split(/\s+/).length).toBe(3);
  });

  it('places the highest value at the top of the plot (smallest y)', () => {
    setup([
      { value: 1 },
      { value: 5 },
      { value: 3 },
    ]);
    const pts = fixture.componentInstance.points();
    const ys = pts.map((p) => p.y);
    // The peak (index 1) is the smallest y.
    expect(Math.min(...ys)).toBeCloseTo(pts[1].y, 3);
  });

  it('renders markers and x labels in full mode', () => {
    const host = setup([
      { value: 1, label: 'Jan' },
      { value: 4, label: 'Feb' },
    ]);
    expect(host.querySelectorAll('circle').length).toBe(2);
    expect(host.textContent).toContain('Jan');
  });

  it('omits markers and labels in sparkline mode', () => {
    const host = setup([{ value: 1 }, { value: 4 }], { sparkline: true });
    expect(host.querySelectorAll('circle').length).toBe(0);
  });

  it('renders a single point without crashing', () => {
    const host = setup([{ value: 3 }]);
    expect(host.querySelector('svg')).toBeTruthy();
    expect(fixture.componentInstance.points().length).toBe(1);
  });

  it('shows an empty state for empty data', () => {
    const host = setup([]);
    expect(host.querySelector('svg')).toBeNull();
    expect(host.textContent).toContain('No trend data');
  });
});
