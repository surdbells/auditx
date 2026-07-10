import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
} from '@angular/core';

import { GanttItem } from '../chart-types';
import { semanticColor } from '../chart-colors';

const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
const DAY_MS = 86_400_000;

/** A month tick on the axis. */
interface GanttMonth {
  x: number;
  label: string;
}

/** A laid-out Gantt row (label + positioned bar). */
interface GanttRow {
  label: string;
  fullLabel: string;
  detail: string;
  color: string;
  y: number;
  barX: number;
  barWidth: number;
}

/** Parse an ISO `yyyy-MM-dd` date to a timezone-independent day number, or null. */
function toDay(iso: string | null | undefined): number | null {
  const parts = iso?.split('-');
  if (!parts || parts.length !== 3) {
    return null;
  }
  const [y, m, d] = parts.map(Number);
  if (!Number.isFinite(y) || !Number.isFinite(m) || !Number.isFinite(d)) {
    return null;
  }
  return Math.floor(Date.UTC(y, m - 1, d) / DAY_MS);
}

/**
 * A dependency-free Gantt / timeline chart. Each item is a horizontal bar spanning its planned start→end within a
 * fixed date range (e.g. an annual plan period), with month gridlines, a "today" marker, and a left label gutter.
 * The SVG uses a fixed viewBox and scales to its container via `width:100%`, so it stays responsive without
 * measuring the DOM. Bar colours resolve from the shared status palette so a "completed" bar matches its badge.
 */
@Component({
  selector: 'app-gantt-chart',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (hasData()) {
      <svg
        [attr.viewBox]="'0 0 ' + width + ' ' + height()"
        class="gantt"
        role="img"
        [attr.aria-label]="ariaLabel()"
        preserveAspectRatio="xMinYMin meet"
      >
        <title>{{ ariaLabel() }}</title>

        @for (mo of months(); track mo.x) {
          <line class="gantt__grid" [attr.x1]="mo.x" [attr.y1]="headerHeight" [attr.x2]="mo.x" [attr.y2]="height()" />
          <text class="gantt__axis" [attr.x]="mo.x + 4" [attr.y]="headerHeight - 9">{{ mo.label }}</text>
        }

        @if (todayX() !== null) {
          <line class="gantt__today" [attr.x1]="todayX()" [attr.y1]="headerHeight" [attr.x2]="todayX()" [attr.y2]="height()" />
        }

        @for (row of rows(); track row.y) {
          <g>
            <text class="gantt__label" [attr.x]="0" [attr.y]="row.y + rowHeight / 2" dominant-baseline="middle">
              {{ row.label }}
              <title>{{ row.fullLabel }}</title>
            </text>
            <rect
              class="gantt__track"
              [attr.x]="labelWidth"
              [attr.y]="row.y + barPadding"
              [attr.width]="timelineWidth"
              [attr.height]="barThickness"
              [attr.rx]="3"
            />
            <rect
              [attr.x]="row.barX"
              [attr.y]="row.y + barPadding"
              [attr.width]="row.barWidth"
              [attr.height]="barThickness"
              [attr.rx]="3"
              [attr.fill]="row.color"
            >
              <title>{{ row.fullLabel }}{{ row.detail ? ' — ' + row.detail : '' }}</title>
            </rect>
          </g>
        }
      </svg>
    } @else {
      <p class="gantt__empty">{{ emptyLabel() }}</p>
    }
  `,
  styles: `
    :host {
      display: block;
      width: 100%;
    }
    .gantt {
      width: 100%;
      height: auto;
      max-width: 100%;
      font-family: var(--mat-sys-body-small-font, sans-serif);
    }
    .gantt__label {
      font-size: 12px;
      fill: var(--mat-sys-on-surface);
    }
    .gantt__axis {
      font-size: 11px;
      fill: var(--mat-sys-on-surface-variant);
    }
    .gantt__grid {
      stroke: var(--mat-sys-outline-variant);
      stroke-width: 1;
    }
    .gantt__track {
      fill: var(--mat-sys-surface-variant);
    }
    .gantt__today {
      stroke: var(--mat-sys-primary);
      stroke-width: 1.5;
      stroke-dasharray: 3 3;
    }
    .gantt__empty {
      margin: 0.5rem 0;
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
    }
  `,
})
export class GanttChartComponent {
  /** The bars to render, in display order. */
  readonly items = input<GanttItem[]>([]);
  /** The timeline's inclusive start / end (ISO `yyyy-MM-dd`) — usually the plan period. */
  readonly rangeStart = input<string>('');
  readonly rangeEnd = input<string>('');
  /** Accessible description + the empty-state message. */
  readonly label = input<string>('Timeline');
  readonly emptyLabel = input<string>('No items to chart.');

  /* ---- Fixed SVG geometry (user units; the SVG scales to fit its box). ---- */
  protected readonly width = 1000;
  protected readonly labelWidth = 210;
  protected readonly headerHeight = 32;
  protected readonly rowHeight = 30;
  protected readonly barPadding = 6;
  protected readonly barThickness = 18;
  protected readonly timelineWidth = this.width - this.labelWidth;

  private readonly startDay = computed(() => toDay(this.rangeStart()));
  private readonly endDay = computed(() => toDay(this.rangeEnd()));

  readonly hasData = computed(() => {
    const s = this.startDay();
    const e = this.endDay();
    return s !== null && e !== null && e > s && this.items().length > 0;
  });

  /** Length of the timeline in days (>= 1). */
  private readonly span = computed(() => {
    const s = this.startDay();
    const e = this.endDay();
    return s !== null && e !== null ? Math.max(1, e - s) : 1;
  });

  private x(day: number): number {
    const s = this.startDay() ?? 0;
    return this.labelWidth + ((day - s) / this.span()) * this.timelineWidth;
  }

  readonly height = computed(
    () => this.headerHeight + Math.max(1, this.items().length) * this.rowHeight,
  );

  readonly rows = computed<GanttRow[]>(() => {
    const s = this.startDay();
    const e = this.endDay();
    if (s === null || e === null) {
      return [];
    }
    return this.items().map((it, i) => {
      const rawStart = toDay(it.start) ?? s;
      const rawEnd = toDay(it.end) ?? rawStart;
      // Clamp the span to the visible range so an out-of-window item still renders at the edge.
      const clampedStart = Math.max(s, Math.min(rawStart, e));
      const clampedEnd = Math.max(clampedStart, Math.min(rawEnd, e));
      const barX = this.x(clampedStart);
      // +1 day so a single-day item still has a visible width; floor at 3 user units.
      const barWidth = Math.max(3, this.x(clampedEnd + 1) - barX);
      return {
        label: this.truncate(it.label),
        fullLabel: it.label,
        detail: it.detail ?? '',
        color: semanticColor(it.tone ?? '') ?? '#1565c0',
        y: this.headerHeight + i * this.rowHeight,
        barX,
        barWidth,
      };
    });
  });

  readonly months = computed<GanttMonth[]>(() => {
    const s = this.startDay();
    const e = this.endDay();
    const parts = this.rangeStart().split('-').map(Number);
    if (s === null || e === null || parts.length !== 3) {
      return [];
    }
    let [year] = parts;
    let month = parts[1]; // 1-based
    const out: GanttMonth[] = [];
    for (let guard = 0; guard < 120; guard++) {
      const day = Math.floor(Date.UTC(year, month - 1, 1) / DAY_MS);
      if (day > e) {
        break;
      }
      if (day >= s) {
        // Show the year on January and on the first tick, otherwise just the month.
        const showYear = month === 1 || out.length === 0;
        out.push({ x: this.x(day), label: showYear ? `${MONTHS[month - 1]} ${year}` : MONTHS[month - 1] });
      }
      month++;
      if (month > 12) {
        month = 1;
        year++;
      }
    }
    return out;
  });

  readonly todayX = computed<number | null>(() => {
    const s = this.startDay();
    const e = this.endDay();
    if (s === null || e === null) {
      return null;
    }
    const now = new Date();
    const today = Math.floor(Date.UTC(now.getFullYear(), now.getMonth(), now.getDate()) / DAY_MS);
    return today < s || today > e ? null : this.x(today);
  });

  readonly ariaLabel = computed(() => `${this.label()}. ${this.items().length} items.`);

  private truncate(value: string): string {
    return value.length > 30 ? `${value.slice(0, 29)}…` : value;
  }
}
