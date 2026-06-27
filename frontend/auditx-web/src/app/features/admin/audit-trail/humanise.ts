/**
 * Render a snake_case actor type (e.g. `itandt_support`) as a human-readable
 * label (`Itandt support`). Safe for any string; empty/falsy values become `—`.
 */
export function humaniseActorType(value: string | null | undefined): string {
  if (!value) {
    return '—';
  }
  const spaced = value.replace(/_/g, ' ');
  return spaced.charAt(0).toUpperCase() + spaced.slice(1);
}

/**
 * Render an event type (e.g. `audit.created`) as a human-readable label
 * (`Audit created`). Splits on `.` / `_` separators. Empty values become `—`.
 */
export function humaniseEventType(value: string | null | undefined): string {
  if (!value) {
    return '—';
  }
  const spaced = value.replace(/[._]/g, ' ');
  return spaced.charAt(0).toUpperCase() + spaced.slice(1);
}

/** Pretty-print a JSON string for display; returns `—` for empty values. */
export function prettyJson(value: string | null | undefined): string {
  if (!value) {
    return '—';
  }
  try {
    return JSON.stringify(JSON.parse(value), null, 2);
  } catch {
    // Not valid JSON — show the raw string rather than failing.
    return value;
  }
}
