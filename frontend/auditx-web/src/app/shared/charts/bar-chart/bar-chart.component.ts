import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
  output,
} from '@angular/core';

import { ChartDatum } from '../chart-types';
import { resolveColor } from '../chart-colors';

/** A bar laid out for rendering (position + resolved colour + scaled length). */
interface LaidOutBar {
  label: string;
  /** Stable identifier for tracking + drilldown clicks (datum key, falling back to the label). */
  key: string;
  value: number;
  color: string;
  /** Top of the bar row band (px, SVG units). */
  y: number;
  /** Length of the filled portion (px), proportional to the max value. */
  length: number;
  /** Baseline label shown at the value end of the bar. */
  valueText: string;
}

/**
 * A dependency-free horizontal bar chart. Bars run left→right; each row shows
 * its label on the left, a proportional coloured bar, and the value at the end.
 * The SVG uses a fixed viewBox and scales to its container via `width:100%`, so
 * it stays responsive without measuring the DOM.
 *
 * Colours resolve from the shared semantic palette (severity/status) with a
 * neutral categorical fallback, so a "critical" bar matches the critical badge.
 * Empty or all-zero data renders a friendly empty state rather than a broken
 * SVG.
 */
@Component({
  selector: 'app-bar-chart',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (hasData()) {
      <svg
        [attr.viewBox]="'0 0 ' + width + ' ' + height()"
        class="chart"
        role="img"
        [attr.aria-label]="ariaLabel()"
        preserveAspectRatio="xMinYMin meet"
      >
        <title>{{ ariaLabel() }}</title>
        @for (bar of bars(); track bar.key) {
          <g
            [class.chart__row--clickable]="clickable()"
            [attr.role]="clickable() ? 'button' : null"
            [attr.tabindex]="clickable() ? 0 : null"
            [attr.aria-label]="clickable() ? bar.label + ': ' + bar.valueText : null"
            (click)="onSegment(bar.key)"
            (keydown.enter)="onSegment(bar.key)"
            (keydown.space)="onSegment(bar.key); $event.preventDefault()"
          >
            <text
              class="chart__label"
              [attr.x]="0"
              [attr.y]="bar.y + rowHeight / 2"
              dominant-baseline="middle"
            >
              {{ bar.label }}
            </text>
            <rect
              class="chart__track"
              [attr.x]="labelWidth"
              [attr.y]="bar.y + barPadding"
              [attr.width]="trackWidth()"
              [attr.height]="barThickness"
              [attr.rx]="barThickness / 2"
            />
            <rect
              [attr.x]="labelWidth"
              [attr.y]="bar.y + barPadding"
              [attr.width]="bar.length"
              [attr.height]="barThickness"
              [attr.rx]="barThickness / 2"
              [attr.fill]="bar.color"
            />
            <text
              class="chart__value"
              [attr.x]="labelWidth + bar.length + 6"
              [attr.y]="bar.y + rowHeight / 2"
              dominant-baseline="middle"
            >
              {{ bar.valueText }}
            </text>
          </g>
        }
      </svg>
    } @else {
      <p class="chart__empty">No data to chart.</p>
    }
  `,
  styles: `
    :host {
      display: block;
      width: 100%;
    }
    .chart {
      width: 100%;
      height: auto;
      max-width: 100%;
      font-family: var(--mat-sys-body-small-font, sans-serif);
    }
    .chart__label {
      font-size: 12px;
      fill: var(--mat-sys-on-surface-variant);
    }
    .chart__value {
      font-size: 12px;
      font-weight: 600;
      fill: var(--mat-sys-on-surface);
    }
    .chart__track {
      fill: var(--mat-sys-surface-variant);
    }
    .chart__empty {
      margin: 0.5rem 0;
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
    }
    .chart__row--clickable {
      cursor: pointer;
      outline: none;
    }
    .chart__row--clickable:hover .chart__label,
    .chart__row--clickable:focus-visible .chart__label {
      text-decoration: underline;
      fill: var(--mat-sys-on-surface);
    }
  `,
})
export class BarChartComponent {
  /** The bars to render, in display order. */
  readonly data = input<ChartDatum[]>([]);
  /** Accessible description; falls back to a generic label. */
  readonly label = input<string>('Bar chart');

  /** When true, bar rows are clickable and emit {@link segmentClick} (drilldown). */
  readonly clickable = input(false);

  /** Emits the clicked bar's label (only when {@link clickable}). */
  readonly segmentClick = output<string>();

  protected onSegment(label: string): void {
    if (this.clickable()) {
      this.segmentClick.emit(label);
    }
  }

  /* ---- Fixed SVG geometry (user units; the SVG scales to fit its box). ---- */
  protected readonly width = 320;
  protected readonly labelWidth = 96;
  protected readonly valueGutter = 40;
  protected readonly rowHeight = 28;
  protected readonly barPadding = 5;
  protected readonly barThickness = 18;
  protected readonly barGap = 6;

  readonly hasData = computed(() =>
    this.data().some((d) => Number.isFinite(d.value) && d.value > 0),
  );

  readonly ariaLabel = computed(() => {
    const parts = this.data()
      .filter((d) => Number.isFinite(d.value))
      .map((d) => `${d.label}: ${d.value}`);
    return parts.length ? `${this.label()}. ${parts.join(', ')}.` : this.label();
  });

  /** Height grows with the number of bars so rows never overlap. */
  readonly height = computed(() =>
    Math.max(this.rowHeight, this.data().length * (this.rowHeight + this.barGap)),
  );

  protected trackWidth(): number {
    return this.width - this.labelWidth - this.valueGutter;
  }

  readonly bars = computed<LaidOutBar[]>(() => {
    const data = this.data().filter((d) => Number.isFinite(d.value));
    const max = Math.max(1, ...data.map((d) => d.value));
    const track = this.trackWidth();
    return data.map((d, i) => ({
      label: d.label,
      key: d.key ?? d.label,
      value: d.value,
      color: resolveColor(d.label, i, d.color),
      y: i * (this.rowHeight + this.barGap),
      length: Math.max(0, (d.value / max) * track),
      valueText: this.formatValue(d.value),
    }));
  });

  private formatValue(value: number): string {
    return Number.isInteger(value) ? String(value) : value.toFixed(1);
  }
}
