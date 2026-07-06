/**
 * Shared colour helpers for the hand-rolled inline-SVG charts.
 *
 * The palette mirrors the semantic severity / status colours defined in
 * `src/styles/_status-badge.scss` so a "critical" slice is the same red as a
 * critical status pill, "high" the same orange, and so on. Keeping the mapping
 * here (rather than hard-coding hexes at each call site) means the charts and
 * the badges stay visually in sync.
 *
 * These are solid foreground hexes (the badge SCSS uses the same hexes at ~16%
 * for their tinted backgrounds); charts want the saturated colour for fills and
 * strokes. Anything unknown falls back through a neutral categorical ramp so a
 * chart is never left with an undefined colour.
 */

/** Semantic severity → solid chart colour (matches `_status-badge.scss`). */
const SEVERITY_COLORS: Record<string, string> = {
  critical: '#c62828',
  high: '#ef6c00',
  medium: '#1565c0',
  low: '#616161',
};

/** Semantic status → solid chart colour (a small, reused subset). */
const STATUS_COLORS: Record<string, string> = {
  completed: '#2e7d32',
  approved: '#2e7d32',
  in_progress: '#1565c0',
  planned: '#616161',
  deferred: '#ef6c00',
  open: '#1565c0',
  closed: '#616161',
  cancelled: '#c62828',
};

/**
 * A neutral categorical ramp for series that carry no semantic meaning
 * (business units, entities, age buckets, …). Distinct, colour-blind-tolerant
 * hues that read well on both light and dark surfaces.
 */
const CATEGORICAL: readonly string[] = [
  '#1565c0',
  '#2e7d32',
  '#ef6c00',
  '#6a1b9a',
  '#00838f',
  '#c62828',
  '#5d4037',
  '#616161',
];

/** Normalise a label to the snake_case key used by the semantic maps. */
function normaliseKey(label: string): string {
  return label.trim().toLowerCase().replace(/[\s-]+/g, '_');
}

/**
 * Resolves a label to a semantic colour when one exists (severity first, then
 * status), otherwise `null` so the caller can fall back to the ramp.
 */
export function semanticColor(label: string): string | null {
  const key = normaliseKey(label);
  return SEVERITY_COLORS[key] ?? STATUS_COLORS[key] ?? null;
}

/** The i-th colour of the neutral categorical ramp (wraps). */
export function categoricalColor(index: number): string {
  return CATEGORICAL[((index % CATEGORICAL.length) + CATEGORICAL.length) % CATEGORICAL.length];
}

/**
 * Picks a colour for a slice/bar: an explicit colour wins, then a semantic
 * match on the label, then the categorical ramp by position.
 */
export function resolveColor(
  label: string,
  index: number,
  explicit?: string,
): string {
  return explicit ?? semanticColor(label) ?? categoricalColor(index);
}
