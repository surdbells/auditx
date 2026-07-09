/**
 * Generic adapters that turn an arbitrary widget `data` payload into the simple
 * shapes the dashboard widgets render — WITHOUT a charting library. Each metric
 * key has a known DTO shape (see analytics.models.ts); these helpers project
 * those into:
 *   - a flat list of key figures (single_metric),
 *   - a column-derived row table (table),
 *   - a labelled numeric series for inline CSS/SVG bars (chart).
 *
 * Everything is defensive: unknown shapes fall back to whatever can be derived,
 * and a `null` payload yields empty results so the widget renders an empty shell.
 */

import { humanise } from './format';

/** A single key-figure for a metric card. */
export interface KeyFigure {
  label: string;
  value: string;
}

/** A labelled numeric bar for the inline chart fallback. */
export interface BarDatum {
  label: string;
  value: number;
}

/** A generic table projection: ordered columns + rows keyed by those columns. */
export interface TableProjection {
  columns: string[];
  headers: string[];
  rows: Record<string, unknown>[];
}

/** Column-shaping options for {@link toTable}: drop id-only columns, override headers. */
export interface TableOptions {
  /** Column keys to omit entirely (e.g. drilldown-only GUID ids). */
  hidden?: ReadonlySet<string>;
  /** Per-column header text, overriding the derived title-cased key. */
  headerOverrides?: Record<string, string>;
}

type Dict = Record<string, unknown>;

function isObject(v: unknown): v is Dict {
  return typeof v === 'object' && v !== null && !Array.isArray(v);
}

function isNumber(v: unknown): v is number {
  return typeof v === 'number' && Number.isFinite(v);
}

/** Pretty-print a primitive cell value; objects/arrays are stringified compactly. */
export function cellText(value: unknown): string {
  if (value === null || value === undefined) {
    return '—';
  }
  if (typeof value === 'number') {
    return Number.isInteger(value) ? String(value) : value.toFixed(1);
  }
  if (typeof value === 'boolean') {
    return value ? 'Yes' : 'No';
  }
  if (typeof value === 'string') {
    return value;
  }
  return JSON.stringify(value);
}

/** Title-cases a camelCase property name into a column header. */
function headerFor(key: string): string {
  const spaced = key
    .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
    .replace(/[._]/g, ' ');
  return spaced.charAt(0).toUpperCase() + spaced.slice(1);
}

/**
 * Returns the array of row-objects a `table` widget should render. Accepts the
 * payload itself being an array (e.g. material findings, scorecards), or an
 * object exposing a single obvious array property (e.g. coverage cells, or a
 * portfolio breakdown). Falls back to a single-row object dump.
 */
export function toTable(data: unknown, options: TableOptions = {}): TableProjection {
  // Coverage matrix special-case: rows/columns/cells → a labelled grid. Handled
  // here (not via toRows) so the column ORDER and headers are explicit — numeric
  // column labels like "2024" would otherwise be reordered ahead of "entity" by
  // JS integer-key enumeration rules.
  if (
    isObject(data) &&
    Array.isArray(data['rows']) &&
    Array.isArray(data['columns']) &&
    Array.isArray(data['cells'])
  ) {
    return coverageProjection(data);
  }

  const hidden = options.hidden ?? EMPTY_SET;
  const overrides = options.headerOverrides ?? {};

  const rows = toRows(data);
  if (rows.length === 0) {
    return { columns: [], headers: [], rows: [] };
  }
  // Union of keys across rows, preserving first-seen order; drop hidden id-only columns.
  const columns: string[] = [];
  for (const row of rows) {
    for (const k of Object.keys(row)) {
      if (!columns.includes(k) && !hidden.has(k)) {
        columns.push(k);
      }
    }
  }
  return { columns, headers: columns.map((c) => overrides[c] ?? headerFor(c)), rows };
}

const EMPTY_SET: ReadonlySet<string> = new Set<string>();

function toRows(data: unknown): Dict[] {
  if (Array.isArray(data)) {
    return data.filter(isObject);
  }
  if (isObject(data)) {
    // Prefer the first array-of-objects property (a breakdown list).
    for (const value of Object.values(data)) {
      if (Array.isArray(value) && value.some(isObject)) {
        return value.filter(isObject);
      }
    }
    // Otherwise treat the object itself as a single row.
    return [data];
  }
  return [];
}

/**
 * Flattens a CoverageMatrix into one row per matrix row. Column keys are
 * prefixed (`c0`, `c1`, …) so numeric labels don't trigger JS integer-key
 * reordering; the matrix's own labels become the headers.
 */
function coverageProjection(data: Dict): TableProjection {
  const rowLabels = data['rows'] as unknown[];
  const colLabels = (data['columns'] as unknown[]).map((c) => String(c));
  const cells = data['cells'] as unknown[][];

  const columns = ['entity', ...colLabels.map((_, j) => `c${j}`)];
  const headers = ['Entity', ...colLabels];
  const rows = rowLabels.map((label, i) => {
    const row: Dict = { entity: String(label) };
    colLabels.forEach((_, j) => {
      row[`c${j}`] = cells[i]?.[j] ?? 0;
    });
    return row;
  });
  return { columns, headers, rows };
}

/**
 * Derives a labelled numeric series for the inline bar chart. Recognises the
 * common breakdown shapes (`{severity|bucket|businessUnit|…, count|openCount}`)
 * and otherwise picks the first numeric field per row against the first
 * string-ish field as the label.
 */
export function toBars(data: unknown): BarDatum[] {
  const rows = Array.isArray(data) ? data.filter(isObject) : firstArray(data);
  return rows
    .map((row) => {
      const label = pickLabel(row);
      const value = pickValue(row);
      return value === null ? null : { label, value };
    })
    .filter((b): b is BarDatum => b !== null);
}

function firstArray(data: unknown): Dict[] {
  if (!isObject(data)) {
    return [];
  }
  for (const value of Object.values(data)) {
    if (Array.isArray(value) && value.some(isObject)) {
      return value.filter(isObject);
    }
  }
  return [];
}

const LABEL_KEYS = [
  'severity',
  'bucket',
  'businessUnit',
  'entityName',
  'category',
  'label',
  'name',
];
const VALUE_KEYS = ['count', 'openCount', 'caseCount', 'value'];

function pickLabel(row: Dict): string {
  for (const k of LABEL_KEYS) {
    if (typeof row[k] === 'string') {
      return humanise(row[k] as string);
    }
  }
  const firstString = Object.values(row).find((v) => typeof v === 'string');
  return typeof firstString === 'string' ? humanise(firstString) : '—';
}

function pickValue(row: Dict): number | null {
  for (const k of VALUE_KEYS) {
    if (isNumber(row[k])) {
      return row[k] as number;
    }
  }
  const firstNumber = Object.values(row).find(isNumber);
  return isNumber(firstNumber) ? firstNumber : null;
}

/** Largest bar value (>= 1) so each bar's width is a percentage of the max. */
export function maxBar(bars: BarDatum[]): number {
  return Math.max(1, ...bars.map((b) => b.value));
}

/**
 * Projects an object payload into flat key figures for a `single_metric` card.
 * Numeric/percent-ish fields are formatted; nested arrays/objects are skipped.
 */
export function toKeyFigures(data: unknown): KeyFigure[] {
  if (!isObject(data)) {
    return [];
  }
  const figures: KeyFigure[] = [];
  for (const [key, value] of Object.entries(data)) {
    if (Array.isArray(value) || isObject(value)) {
      continue;
    }
    const label = headerFor(key);
    if (isNumber(value)) {
      figures.push({
        label,
        value: /percent/i.test(key) ? `${value.toFixed(1)}%` : cellText(value),
      });
    } else {
      figures.push({ label, value: cellText(value) });
    }
  }
  return figures;
}
