import { RiskBand } from '../../core/models';

/** Derives the severity band from a likelihood×impact score (1–25), mirroring the backend RiskBands. */
export function bandOf(score: number): RiskBand {
  if (score <= 4) {
    return 'low';
  }
  if (score <= 9) {
    return 'medium';
  }
  if (score <= 15) {
    return 'high';
  }
  return 'critical';
}

/** Background colour per band for heatmap cells + band chips (semantic, not the app accent). */
export const BAND_COLOR: Record<RiskBand, string> = {
  low: '#2e7d32',
  medium: '#f9a825',
  high: '#ef6c00',
  critical: '#c62828',
};
