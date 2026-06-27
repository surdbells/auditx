/**
 * Render a report status / distribution outcome value (e.g. `pending`,
 * `in_progress`, `bounced`) as a human-readable label — splitting on `_` and
 * `.` separators. Empty/falsy values become `—`.
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
