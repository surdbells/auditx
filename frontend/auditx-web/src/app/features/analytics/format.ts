/**
 * Small display helpers shared across the M9 analytics components.
 */

/**
 * Render a snake_case / dotted enum value (e.g. `single_metric`, `in_progress`)
 * as a human-readable label. Empty/falsy values become `—`.
 */
export function humanise(value: string | null | undefined): string {
  if (!value) {
    return '—';
  }
  const spaced = value.replace(/[._]/g, ' ');
  return spaced.charAt(0).toUpperCase() + spaced.slice(1);
}

/** Render a number-or-null metric, falling back to `—`. */
export function metricOrDash(value: number | null | undefined): string {
  return value === null || value === undefined ? '—' : String(value);
}

/** Render a percentage (already 0–100) with one decimal place, or `—`. */
export function percent(value: number | null | undefined): string {
  return value === null || value === undefined ? '—' : `${value.toFixed(1)}%`;
}

/** Render an average-days metric to one decimal place, or `—`. */
export function days(value: number | null | undefined): string {
  return value === null || value === undefined ? '—' : value.toFixed(1);
}
