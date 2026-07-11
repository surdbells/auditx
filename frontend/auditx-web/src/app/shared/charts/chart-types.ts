/**
 * Shared datum shapes for the inline-SVG chart components. A `ChartDatum` is the
 * single categorical entry consumed by the bar and donut charts; `PointDatum`
 * feeds the line chart / sparkline.
 */

/** One labelled value for a bar or donut segment. Colour is optional. */
export interface ChartDatum {
  label: string;
  value: number;
  /** Explicit colour override; otherwise resolved from the semantic palette. */
  color?: string;
  /**
   * Stable identifier emitted by drilldown clicks (falls back to {@link label}). Set it when labels are
   * humanised/display-only so a click carries the raw code and distinct data can never collide on a label.
   */
  key?: string;
}

/** One point on a line chart / sparkline. */
export interface PointDatum {
  /** The x label (categorical or ordinal); shown on the axis when present. */
  label?: string;
  value: number;
}

/** One bar on the Gantt timeline: a labelled date span with an optional semantic tone. */
export interface GanttItem {
  /** Row label (e.g. entity name). */
  label: string;
  /** Inclusive start date, ISO `yyyy-MM-dd`. */
  start: string;
  /** Inclusive end date, ISO `yyyy-MM-dd`. */
  end: string;
  /** Semantic status/tone for the bar colour (resolved via the shared status palette). */
  tone?: string;
  /** Optional secondary text for the bar tooltip (e.g. audit type + dates). */
  detail?: string;
}
