import {
  cellText,
  maxBar,
  toBars,
  toKeyFigures,
  toTable,
} from './widget-data';

describe('widget-data projections', () => {
  describe('toKeyFigures', () => {
    it('formats numbers, percents and skips nested structures', () => {
      const figures = toKeyFigures({
        auditsInFlight: 2,
        planExecutionPercent: 70,
        nested: { x: 1 },
        list: [1, 2],
      });
      const labels = figures.map((f) => f.label);
      expect(labels).toContain('Audits In Flight');
      expect(labels).not.toContain('Nested');
      expect(labels).not.toContain('List');
      const pct = figures.find((f) => f.label === 'Plan Execution Percent');
      expect(pct?.value).toBe('70.0%');
    });

    it('returns nothing for a null payload', () => {
      expect(toKeyFigures(null)).toEqual([]);
    });
  });

  describe('toTable', () => {
    it('unions row keys and derives headers from an array payload', () => {
      const t = toTable([
        { businessUnit: 'Retail', caseCount: 4 },
        { businessUnit: 'Corporate', appealRatePercent: 10 },
      ]);
      expect(t.columns).toEqual(['businessUnit', 'caseCount', 'appealRatePercent']);
      expect(t.headers[0]).toBe('Business Unit');
      expect(t.rows.length).toBe(2);
    });

    it('flattens a coverage matrix into labelled rows', () => {
      const t = toTable({
        rows: ['Retail', 'Corporate'],
        columns: ['2024', '2025'],
        cells: [
          [1, 2],
          [0, 3],
        ],
      });
      expect(t.columns).toEqual(['entity', 'c0', 'c1']);
      expect(t.headers).toEqual(['Entity', '2024', '2025']);
      expect(t.rows[0]['entity']).toBe('Retail');
      expect(t.rows[1]['c1']).toBe(3);
    });

    it('picks the first array-of-objects property from an object payload', () => {
      const t = toTable({
        totalOpen: 5,
        bySeverity: [{ severity: 'high', count: 3 }],
      });
      expect(t.rows.length).toBe(1);
      expect(t.columns).toContain('severity');
    });

    it('returns an empty projection for a null payload', () => {
      expect(toTable(null).rows).toEqual([]);
    });
  });

  describe('toBars', () => {
    it('derives labelled bars from a breakdown object', () => {
      const bars = toBars({
        byAgeBucket: [
          { bucket: '0_30', count: 4 },
          { bucket: '31_60', count: 1 },
        ],
      });
      expect(bars).toEqual([
        { label: '0 30', value: 4 },
        { label: '31 60', value: 1 },
      ]);
      expect(maxBar(bars)).toBe(4);
    });

    it('returns no bars when no numeric series can be derived', () => {
      expect(toBars({ note: 'nothing here' })).toEqual([]);
      expect(toBars(null)).toEqual([]);
    });
  });

  describe('cellText', () => {
    it('renders primitives and falls back to a dash for null', () => {
      expect(cellText(null)).toBe('—');
      expect(cellText(3)).toBe('3');
      expect(cellText(2.345)).toBe('2.3');
      expect(cellText(true)).toBe('Yes');
      expect(cellText('hello')).toBe('hello');
    });
  });
});
