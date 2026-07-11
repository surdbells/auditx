import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
  output,
} from '@angular/core';

import { ChartDatum } from '../chart-types';
import { resolveColor } from '../chart-colors';

/** One donut ring segment, laid out as a stroked circle arc. */
interface Segment {
  label: string;
  /** Stable identifier for tracking + drilldown clicks (datum key, falling back to the label). */
  key: string;
  value: number;
  color: string;
  /** Length of this arc along the circumference (px). */
  dash: number;
  /** Offset that rotates this arc to start where the previous one ended (px). */
  offset: number;
  /** Share of the total, 0–100, for the legend. */
  percent: number;
}

/**
 * A dependency-free donut chart. Segments are drawn as a single `<circle>` per
 * slice using `stroke-dasharray` / `stroke-dashoffset`, so there is no arc-path
 * trigonometry to get wrong and the ring is always closed. The centre shows the
 * total; a compact legend lists each slice with its colour, label and share.
 *
 * Colours come from the shared semantic palette (a "critical" slice is the same
 * red as the critical badge). Empty / all-zero data renders an empty state.
 */
@Component({
  selector: 'app-donut-chart',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (hasData()) {
      <div class="donut">
        <svg
          viewBox="0 0 42 42"
          class="donut__svg"
          role="img"
          [attr.aria-label]="ariaLabel()"
        >
          <title>{{ ariaLabel() }}</title>
          <circle
            class="donut__ring"
            cx="21"
            cy="21"
            [attr.r]="radius"
            fill="transparent"
            [attr.stroke-width]="thickness"
          />
          <!--
            NOTE: the slices are deliberately NOT click targets. Each slice is a full transparent-fill circle,
            so under SVG hit-testing the topmost circle would swallow every click in the ring AND the hole —
            drilling into the wrong segment. The legend rows below are the (keyboard-accessible) drill surface.
          -->
          @for (seg of segments(); track seg.key) {
            <circle
              cx="21"
              cy="21"
              [attr.r]="radius"
              fill="transparent"
              pointer-events="none"
              [attr.stroke]="seg.color"
              [attr.stroke-width]="thickness"
              [attr.stroke-dasharray]="seg.dash + ' ' + (circumference - seg.dash)"
              [attr.stroke-dashoffset]="seg.offset"
              transform="rotate(-90 21 21)"
            />
          }
          <text
            x="21"
            y="20"
            class="donut__total"
            text-anchor="middle"
            dominant-baseline="middle"
          >
            {{ total() }}
          </text>
          <text
            x="21"
            y="26"
            class="donut__caption"
            text-anchor="middle"
            dominant-baseline="middle"
          >
            {{ centerLabel() }}
          </text>
        </svg>
        <ul class="donut__legend">
          @for (seg of segments(); track seg.key) {
            <li
              class="donut__legend-item"
              [class.donut__legend-item--clickable]="clickable()"
              [attr.role]="clickable() ? 'button' : null"
              [attr.tabindex]="clickable() ? 0 : null"
              (click)="onSegment(seg.key)"
              (keydown.enter)="onSegment(seg.key)"
              (keydown.space)="onSegment(seg.key); $event.preventDefault()"
            >
              <span class="donut__swatch" [style.background]="seg.color"></span>
              <span class="donut__legend-label">{{ seg.label }}</span>
              <span class="donut__legend-value">
                {{ seg.value }} ({{ seg.percent }}%)
              </span>
            </li>
          }
        </ul>
      </div>
    } @else {
      <p class="chart__empty">No data to chart.</p>
    }
  `,
  styles: `
    :host {
      display: block;
      width: 100%;
    }
    .donut {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 1rem;
    }
    .donut__svg {
      width: 140px;
      height: 140px;
      max-width: 100%;
      flex: 0 0 auto;
    }
    .donut__ring {
      stroke: var(--mat-sys-surface-variant);
    }
    .donut__total {
      font-size: 8px;
      font-weight: 700;
      fill: var(--mat-sys-on-surface);
    }
    .donut__caption {
      font-size: 3px;
      fill: var(--mat-sys-on-surface-variant);
      text-transform: uppercase;
      letter-spacing: 0.08em;
    }
    .donut__legend {
      list-style: none;
      margin: 0;
      padding: 0;
      flex: 1 1 140px;
      min-width: 140px;
      display: flex;
      flex-direction: column;
      gap: 0.35rem;
    }
    .donut__legend-item {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      font: var(--mat-sys-body-small);
    }
    .donut__swatch {
      width: 12px;
      height: 12px;
      border-radius: 3px;
      flex: 0 0 auto;
    }
    .donut__legend-label {
      flex: 1 1 auto;
      color: var(--mat-sys-on-surface);
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .donut__legend-value {
      color: var(--mat-sys-on-surface-variant);
      white-space: nowrap;
    }
    .donut__legend-item--clickable {
      cursor: pointer;
      outline: none;
      border-radius: 4px;
    }
    .donut__legend-item--clickable:hover .donut__legend-label,
    .donut__legend-item--clickable:focus-visible .donut__legend-label {
      text-decoration: underline;
    }
    .donut__legend-item--clickable:focus-visible {
      outline: 2px solid var(--mat-sys-primary);
      outline-offset: 1px;
    }
    .chart__empty {
      margin: 0.5rem 0;
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
    }
  `,
})
export class DonutChartComponent {
  /** The segments to render, in ring order. */
  readonly data = input<ChartDatum[]>([]);
  /** Accessible description; falls back to a generic label. */
  readonly label = input<string>('Donut chart');
  /** Small caption under the centre total (e.g. "total", "open"). */
  readonly centerLabel = input<string>('total');

  /** When true, slices/legend rows are clickable and emit {@link segmentClick} (drilldown). */
  readonly clickable = input(false);

  /** Emits the clicked segment's label (only when {@link clickable}). */
  readonly segmentClick = output<string>();

  protected onSegment(label: string): void {
    if (this.clickable()) {
      this.segmentClick.emit(label);
    }
  }

  /* ---- Ring geometry in the 42×42 viewBox (radius chosen so the ring fits). */
  protected readonly radius = 15.9155; // circumference ≈ 100 → dash values read as %.
  protected readonly thickness = 6;
  protected readonly circumference = 2 * Math.PI * 15.9155;

  readonly hasData = computed(() =>
    this.data().some((d) => Number.isFinite(d.value) && d.value > 0),
  );

  readonly total = computed(() =>
    this.data().reduce(
      (sum, d) => sum + (Number.isFinite(d.value) ? d.value : 0),
      0,
    ),
  );

  readonly ariaLabel = computed(() => {
    const parts = this.data()
      .filter((d) => Number.isFinite(d.value) && d.value > 0)
      .map((d) => `${d.label}: ${d.value}`);
    return parts.length
      ? `${this.label()}. Total ${this.total()}. ${parts.join(', ')}.`
      : this.label();
  });

  readonly segments = computed<Segment[]>(() => {
    const data = this.data().filter(
      (d) => Number.isFinite(d.value) && d.value > 0,
    );
    const total = data.reduce((sum, d) => sum + d.value, 0) || 1;
    const c = this.circumference;
    let cursor = 0;
    return data.map((d, i) => {
      const fraction = d.value / total;
      const dash = fraction * c;
      // stroke-dashoffset walks backwards around the ring; negate the cursor so
      // each segment begins where the previous one ended.
      const seg: Segment = {
        label: d.label,
        key: d.key ?? d.label,
        value: d.value,
        color: resolveColor(d.label, i, d.color),
        dash,
        offset: c - cursor,
        percent: Math.round(fraction * 100),
      };
      cursor += dash;
      return seg;
    });
  });
}
