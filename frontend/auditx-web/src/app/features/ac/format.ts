/**
 * Small display helpers shared across the M13 AC-workspace components.
 */

/**
 * Render a snake_case / dotted enum value (e.g. `pending_review`, `in_progress`)
 * as a human-readable label. Empty/falsy values become `—`.
 */
export function humanise(value: string | null | undefined): string {
  if (!value) {
    return '—';
  }
  const spaced = value.replace(/[._]/g, ' ');
  return spaced.charAt(0).toUpperCase() + spaced.slice(1);
}

/** Shorten a hash for compact display; returns `—` for empty values. */
export function shortHash(value: string | null | undefined): string {
  if (!value) {
    return '—';
  }
  return value.length > 12 ? `${value.slice(0, 12)}…` : value;
}

/** Render a percentage (already 0–100) with one decimal place, or `—`. */
export function percent(value: number | null | undefined): string {
  return value === null || value === undefined ? '—' : `${value.toFixed(1)}%`;
}

/** Render an average-days metric to one decimal place, or `—`. */
export function days(value: number | null | undefined): string {
  return value === null || value === undefined ? '—' : value.toFixed(1);
}