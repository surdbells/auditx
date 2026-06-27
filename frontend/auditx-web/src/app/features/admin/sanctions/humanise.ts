/**
 * Render a snake_case sanctions enum value (status, severity, HR outcome type,
 * DC decision, appeal outcome) as a human-readable label — e.g.
 * `recommendation_drafted` → `Recommendation drafted`, `dc_referral` →
 * `Dc referral`. Empty/falsy values become `—`.
 */
export function humanise(value: string | null | undefined): string {
  if (!value) {
    return '—';
  }
  const spaced = value.replace(/_/g, ' ');
  return spaced.charAt(0).toUpperCase() + spaced.slice(1);
}
