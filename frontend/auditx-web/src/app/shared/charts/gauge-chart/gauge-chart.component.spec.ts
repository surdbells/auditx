import { ComponentFixture, TestBed } from '@angular/core/testing';

import { GaugeChartComponent } from './gauge-chart.component';

describe('GaugeChartComponent', () => {
  let fixture: ComponentFixture<GaugeChartComponent>;

  function setup(value: number | null, label = 'Plan execution'): HTMLElement {
    TestBed.configureTestingModule({ imports: [GaugeChartComponent] });
    fixture = TestBed.createComponent(GaugeChartComponent);
    fixture.componentRef.setInput('value', value);
    fixture.componentRef.setInput('label', label);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('renders the arc paths and the rounded percentage', () => {
    const host = setup(70);
    expect(host.querySelector('svg')).toBeTruthy();
    // A track arc + a progress arc.
    expect(host.querySelectorAll('path').length).toBe(2);
    expect(host.textContent).toContain('70%');
    expect(host.textContent).toContain('Plan execution');
  });

  it('clamps values into 0–100', () => {
    setup(140);
    expect(fixture.componentInstance.clamped()).toBe(100);
    fixture.componentRef.setInput('value', -10);
    expect(fixture.componentInstance.clamped()).toBe(0);
  });

  it('reveals the progress arc proportionally to the value', () => {
    setup(50);
    const half = fixture.componentInstance.arcLength / 2;
    expect(fixture.componentInstance.dashOffset()).toBeCloseTo(half, 3);
  });

  it('bands the colour by value: red / amber / green', () => {
    setup(40);
    expect(fixture.componentInstance.color()).toBe('#c62828');
    fixture.componentRef.setInput('value', 60);
    expect(fixture.componentInstance.color()).toBe('#ef6c00');
    fixture.componentRef.setInput('value', 90);
    expect(fixture.componentInstance.color()).toBe('#2e7d32');
  });

  it('shows an empty state when the value is null', () => {
    const host = setup(null);
    expect(host.querySelector('svg')).toBeNull();
    expect(host.textContent).toContain('No data');
  });
});
