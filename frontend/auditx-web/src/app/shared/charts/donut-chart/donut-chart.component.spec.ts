import { ComponentFixture, TestBed } from '@angular/core/testing';

import { DonutChartComponent } from './donut-chart.component';
import { ChartDatum } from '../chart-types';

describe('DonutChartComponent', () => {
  let fixture: ComponentFixture<DonutChartComponent>;

  function setup(data: ChartDatum[], label = 'Severity'): HTMLElement {
    TestBed.configureTestingModule({ imports: [DonutChartComponent] });
    fixture = TestBed.createComponent(DonutChartComponent);
    fixture.componentRef.setInput('data', data);
    fixture.componentRef.setInput('label', label);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('renders one arc circle per segment plus the background ring', () => {
    const host = setup([
      { label: 'Critical', value: 2 },
      { label: 'High', value: 3 },
    ]);
    expect(host.querySelector('svg')).toBeTruthy();
    // 1 background ring + 2 segment arcs.
    expect(host.querySelectorAll('circle').length).toBe(3);
  });

  it('shows the total in the centre and a legend row per segment', () => {
    const host = setup([
      { label: 'Critical', value: 2 },
      { label: 'High', value: 3 },
    ]);
    expect(fixture.componentInstance.total()).toBe(5);
    expect(host.textContent).toContain('5');
    expect(host.querySelectorAll('.donut__legend-item').length).toBe(2);
  });

  it('computes each segment share as a percentage', () => {
    setup([
      { label: 'A', value: 3 },
      { label: 'B', value: 1 },
    ]);
    const segs = fixture.componentInstance.segments();
    expect(segs[0].percent).toBe(75);
    expect(segs[1].percent).toBe(25);
  });

  it('resolves semantic colours for severity labels', () => {
    setup([
      { label: 'Critical', value: 1 },
      { label: 'Medium', value: 1 },
    ]);
    const segs = fixture.componentInstance.segments();
    expect(segs[0].color).toBe('#c62828');
    expect(segs[1].color).toBe('#1565c0');
  });

  it('ignores zero-value entries when building segments', () => {
    setup([
      { label: 'A', value: 4 },
      { label: 'B', value: 0 },
    ]);
    expect(fixture.componentInstance.segments().length).toBe(1);
  });

  it('shows an empty state for empty data', () => {
    const host = setup([]);
    expect(host.querySelector('svg')).toBeNull();
    expect(host.textContent).toContain('No data');
  });
});
