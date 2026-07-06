import {
  categoricalColor,
  resolveColor,
  semanticColor,
} from './chart-colors';

describe('chart-colors', () => {
  describe('semanticColor', () => {
    it('maps severities to the badge palette (case / spacing insensitive)', () => {
      expect(semanticColor('critical')).toBe('#c62828');
      expect(semanticColor('High')).toBe('#ef6c00');
      expect(semanticColor('MEDIUM')).toBe('#1565c0');
      expect(semanticColor('low')).toBe('#616161');
    });

    it('maps a known status label', () => {
      expect(semanticColor('In progress')).toBe('#1565c0');
      expect(semanticColor('completed')).toBe('#2e7d32');
    });

    it('returns null for an unknown label', () => {
      expect(semanticColor('Retail banking')).toBeNull();
    });
  });

  describe('categoricalColor', () => {
    it('wraps around the ramp and never returns undefined', () => {
      const first = categoricalColor(0);
      expect(first).toMatch(/^#/);
      // Index 8 wraps back to index 0 (ramp length 8).
      expect(categoricalColor(8)).toBe(first);
    });
  });

  describe('resolveColor', () => {
    it('prefers an explicit override', () => {
      expect(resolveColor('critical', 0, '#abcdef')).toBe('#abcdef');
    });

    it('falls back to the semantic colour, then the ramp', () => {
      expect(resolveColor('high', 3)).toBe('#ef6c00');
      expect(resolveColor('Corporate', 0)).toBe(categoricalColor(0));
    });
  });
});
