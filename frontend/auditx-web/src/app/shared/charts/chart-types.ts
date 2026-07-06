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
}

/** One point on a line chart / sparkline. */
export interface PointDatum {
  /** The x label (categorical or ordinal); shown on the axis when present. */
  label?: string;
  value: number;
}
