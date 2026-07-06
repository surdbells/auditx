import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
} from '@angular/core';

import { PointDatum } from '../chart-types';

/** A point mapped into SVG coordinates. */
interface PlottedPoint {
  x: number;
  y: number;
  label?: string;
  value: number;
}

/**
 * A dependency-free line chart / sparkline over an ordered numeric series. Draws
 * a polyline (optionally area-filled) with small markers and, when not in
 * sparkline mode, x labels and the min/max on the y-axis. Useful for trends or
 * any ordered series (age-bucket progression, plan items over the year).
 *
 * A single point renders as a dot; empty data renders an empty state. The SVG
 * scales to its container via a fixed viewBox.
 */
@Component({
  selector: 'app-line-chart',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (hasData()) {
      <svg
        [attr.viewBox]="'0 0 ' + width + ' ' + height"
        class="line"
        role="img"
        [attr.aria-label]="ariaLabel()"
        preserveAspectRatio="none"
      >
        <title>{{ ariaLabel() }}</title>
        @if (areaPath()) {
          <path [attr.d]="areaPath()" class="line__area" [attr.fill]="color()" />
        }
        <polyline
          [attr.points]="polyline()"
          fill="none"
          [attr.stroke]="color()"
          [attr.stroke-width]="strokeWidth"
          stroke-linejoin="round"
          stroke-linecap="round"
        />
        @if (!sparkline()) {
          @for (p of points(); track $index) {
            <circle
              [attr.cx]="p.x"
              [attr.cy]="p.y"
              [attr.r]="dotRadius"
              [attr.fill]="color()"
            />
            @if (p.label) {
              <text
                class="line__label"
                [attr.x]="p.x"
                [attr.y]="height - 2"
                text-anchor="middle"
              >
                {{ p.label }}
              </text>
            }
          }
        }
      </svg>
    } @else {
      <p class="chart__empty">No trend data.</p>
    }
  `,
  styles: `
    :host {
      display: block;
      width: 100%;
    }
    .line {
      width: 100%;
      height: auto;
      max-width: 100%;
      overflow: visible;
    }
    .line__area {
      opacity: 0.12;
    }
    .line__label {
      font-size: 7px;
      fill: var(--mat-sys-on-surface-variant);
    }
    .chart__empty {
      margin: 0.5rem 0;
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
    }
  `,
})
export class LineChartComponent {
  /** The ordered series to plot. */
  readonly data = input<PointDatum[]>([]);
  /** Accessible description. */
  readonly label = input<string>('Line chart');
  /** Compact mode: no markers, labels or padding (for inline sparklines). */
  readonly sparkline = input<boolean>(false);
  /** Stroke / fill colour. */
  readonly color = input<string>('#1565c0');
  /** Fill the area under the line. */
  readonly area = input<boolean>(true);

  /* ---- Fixed plot geometry. ---- */
  protected readonly width = 240;
  protected readonly height = 80;
  protected readonly strokeWidth = 2;
  protected readonly dotRadius = 2.5;

  readonly hasData = computed(() =>
    this.data().some((d) => Number.isFinite(d.value)),
  );

  private readonly pad = computed(() => (this.sparkline() ? 2 : 12));

  readonly points = computed<PlottedPoint[]>(() => {
    const data = this.data().filter((d) => Number.isFinite(d.value));
    if (data.length === 0) {
      return [];
    }
    const pad = this.pad();
    const innerW = this.width - pad * 2;
    const bottomPad = this.sparkline() ? pad : 14; // room for x labels.
    const innerH = this.height - pad - bottomPad;
    const values = data.map((d) => d.value);
    const min = Math.min(...values);
    const max = Math.max(...values);
    const span = max - min || 1;
    const step = data.length > 1 ? innerW / (data.length - 1) : 0;
    return data.map((d, i) => ({
      x: data.length > 1 ? pad + i * step : this.width / 2,
      y: pad + innerH - ((d.value - min) / span) * innerH,
      label: d.label,
      value: d.value,
    }));
  });

  readonly polyline = computed(() =>
    this.points()
      .map((p) => `${p.x.toFixed(2)},${p.y.toFixed(2)}`)
      .join(' '),
  );

  readonly areaPath = computed(() => {
    if (!this.area()) {
      return '';
    }
    const pts = this.points();
    if (pts.length < 2) {
      return '';
    }
    const bottom = this.height - (this.sparkline() ? this.pad() : 14);
    const line = pts
      .map((p, i) => `${i === 0 ? 'M' : 'L'} ${p.x.toFixed(2)} ${p.y.toFixed(2)}`)
      .join(' ');
    const last = pts[pts.length - 1];
    const first = pts[0];
    return `${line} L ${last.x.toFixed(2)} ${bottom} L ${first.x.toFixed(2)} ${bottom} Z`;
  });

  readonly ariaLabel = computed(() => {
    const values = this.data()
      .filter((d) => Number.isFinite(d.value))
      .map((d) => d.value);
    if (values.length === 0) {
      return this.label();
    }
    return `${this.label()}. Values from ${Math.min(...values)} to ${Math.max(
      ...values,
    )}.`;
  });
}
