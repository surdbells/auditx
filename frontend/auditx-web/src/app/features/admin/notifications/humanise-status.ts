/**
 * Render a snake_case dispatch status (e.g. `dead_letter`) as a human-readable
 * label (`Dead letter`). Safe for any string; empty/falsy values become `—`.
 */
export function humaniseStatus(value: string | null | undefined): string {
  if (!value) {
    return '—';
  }
  const spaced = value.replace(/_/g, ' ');
  return spaced.charAt(0).toUpperCase() + spaced.slice(1);
}
