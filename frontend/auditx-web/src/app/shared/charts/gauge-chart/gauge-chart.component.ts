import {
  ChangeDetectionStrategy,
  Component,
  computed,
  input,
} from '@angular/core';

/**
 * A dependency-free semicircular gauge for a single 0–100% value (plan
 * execution, closure rate, grid adherence, …). The arc is drawn as two SVG
 * paths — a full grey track and a coloured progress arc clipped by
 * `stroke-dasharray` — with the percentage and a caption in the centre.
 *
 * The fill colour steps through the semantic palette by band (red < 50,
 * amber < 75, green ≥ 75) so a low adherence reads as a warning without extra
 * config. Values are clamped to 0–100 and a null value renders an empty state.
 */
@Component({
  selector: 'app-gauge-chart',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (hasValue()) {
      <svg
        viewBox="0 0 120 72"
        class="gauge"
        role="img"
        [attr.aria-label]="ariaLabel()"
        preserveAspectRatio="xMidYMid meet"
      >
        <title>{{ ariaLabel() }}</title>
        <path
          class="gauge__track"
          [attr.d]="arcPath"
          fill="none"
          [attr.stroke-width]="thickness"
          stroke-linecap="round"
        />
        <path
          [attr.d]="arcPath"
          fill="none"
          [attr.stroke]="color()"
          [attr.stroke-width]="thickness"
          stroke-linecap="round"
          [attr.stroke-dasharray]="arcLength"
          [attr.stroke-dashoffset]="dashOffset()"
        />
        <text
          x="60"
          y="52"
          class="gauge__value"
          text-anchor="middle"
          [attr.fill]="color()"
        >
          {{ display() }}
        </text>
        @if (label()) {
          <text x="60" y="66" class="gauge__label" text-anchor="middle">
            {{ label() }}
          </text>
        }
      </svg>
    } @else {
      <p class="chart__empty">No data.</p>
    }
  `,
  styles: `
    :host {
      display: block;
      width: 100%;
    }
    .gauge {
      width: 100%;
      height: auto;
      max-width: 220px;
    }
    .gauge__track {
      stroke: var(--mat-sys-surface-variant);
    }
    .gauge__value {
      font-size: 20px;
      font-weight: 700;
      font-family: var(--mat-sys-body-large-font, sans-serif);
    }
    .gauge__label {
      font-size: 8px;
      fill: var(--mat-sys-on-surface-variant);
      font-family: var(--mat-sys-body-small-font, sans-serif);
    }
    .chart__empty {
      margin: 0.5rem 0;
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
    }
  `,
})
export class GaugeChartComponent {
  /** The 0–100 value; `null`/absent → empty state. */
  readonly value = input<number | null>(null);
  /** Caption under the percentage (e.g. "Plan execution"). */
  readonly label = input<string>('');

  /* ---- Semicircle geometry (120×72 viewBox, arc from left to right). ---- */
  protected readonly thickness = 12;
  /** Path: a 180° arc of radius 48 centred at (60,60), swept left→right. */
  protected readonly arcPath = 'M 12 60 A 48 48 0 0 1 108 60';
  /** Arc length = π·r for a semicircle (r = 48). Public so tests can assert it. */
  readonly arcLength = Math.PI * 48;

  readonly hasValue = computed(() => {
    const v = this.value();
    return v !== null && v !== undefined && Number.isFinite(v);
  });

  /** The value clamped into 0–100. */
  readonly clamped = computed(() => {
    const v = this.value() ?? 0;
    return Math.max(0, Math.min(100, v));
  });

  readonly display = computed(() => `${Math.round(this.clamped())}%`);

  readonly ariaLabel = computed(() => {
    const caption = this.label() ? `${this.label()}: ` : '';
    return `${caption}${this.clamped().toFixed(0)} percent`;
  });

  /** Reveal the coloured arc proportionally to the value. */
  readonly dashOffset = computed(
    () => this.arcLength * (1 - this.clamped() / 100),
  );

  /** Band-based colour: red < 50, amber < 75, green ≥ 75. */
  readonly color = computed(() => {
    const v = this.clamped();
    if (v < 50) {
      return '#c62828';
    }
    if (v < 75) {
      return '#ef6c00';
    }
    return '#2e7d32';
  });
}
