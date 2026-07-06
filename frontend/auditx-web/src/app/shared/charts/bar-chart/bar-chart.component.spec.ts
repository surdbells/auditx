import { ComponentFixture, TestBed } from '@angular/core/testing';

import { BarChartComponent } from './bar-chart.component';
import { ChartDatum } from '../chart-types';

describe('BarChartComponent', () => {
  let fixture: ComponentFixture<BarChartComponent>;

  function setup(data: ChartDatum[], label = 'Bars'): HTMLElement {
    TestBed.configureTestingModule({ imports: [BarChartComponent] });
    fixture = TestBed.createComponent(BarChartComponent);
    fixture.componentRef.setInput('data', data);
    fixture.componentRef.setInput('label', label);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('renders one filled bar (plus a track) per datum', () => {
    const host = setup([
      { label: 'A', value: 4 },
      { label: 'B', value: 1 },
    ]);
    expect(host.querySelector('svg')).toBeTruthy();
    // 2 tracks + 2 fills = 4 rects.
    expect(host.querySelectorAll('rect').length).toBe(4);
    expect(host.querySelectorAll('text').length).toBeGreaterThan(0);
  });

  it('scales bar length proportionally to the max value', () => {
    setup([
      { label: 'A', value: 10 },
      { label: 'B', value: 5 },
    ]);
    const bars = fixture.componentInstance.bars();
    // The largest bar fills the whole track; half-value is half the length.
    expect(bars[1].length).toBeCloseTo(bars[0].length / 2, 1);
  });

  it('resolves a semantic colour for a severity label', () => {
    setup([{ label: 'Critical', value: 3 }]);
    expect(fixture.componentInstance.bars()[0].color).toBe('#c62828');
  });

  it('honours an explicit colour override', () => {
    setup([{ label: 'A', value: 3, color: '#123456' }]);
    expect(fixture.componentInstance.bars()[0].color).toBe('#123456');
  });

  it('builds an aria-label describing the series', () => {
    const host = setup(
      [
        { label: 'A', value: 4 },
        { label: 'B', value: 1 },
      ],
      'Ages',
    );
    const svg = host.querySelector('svg');
    expect(svg?.getAttribute('aria-label')).toContain('Ages');
    expect(svg?.getAttribute('aria-label')).toContain('A: 4');
    expect(svg?.querySelector('title')?.textContent).toContain('Ages');
  });

  it('shows an empty state for empty data', () => {
    const host = setup([]);
    expect(host.querySelector('svg')).toBeNull();
    expect(host.textContent).toContain('No data');
  });

  it('shows an empty state when all values are zero', () => {
    const host = setup([
      { label: 'A', value: 0 },
      { label: 'B', value: 0 },
    ]);
    expect(host.querySelector('svg')).toBeNull();
    expect(fixture.componentInstance.hasData()).toBe(false);
  });
});
