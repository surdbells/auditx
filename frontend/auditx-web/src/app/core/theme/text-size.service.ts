import { Injectable, signal } from '@angular/core';

/** Selectable page text sizes; scales the root font-size so all rem-based type follows. */
export type TextSize = 'sm' | 'md' | 'lg' | 'xl';

const STORAGE_KEY = 'auditx.textSize';

/** Root font-size per step (100% = the browser default 16px). */
const SCALE: Record<TextSize, string> = {
  sm: '87.5%',
  md: '100%',
  lg: '112.5%',
  xl: '125%',
};

/**
 * Persists the user's page text size and applies it by setting `--ax-root-font` on the document
 * root; `styles.scss` binds `html { font-size: var(--ax-root-font) }`, so every rem-based size
 * scales at once. Mirrors {@link TranslationService}'s signal + localStorage shape.
 */
@Injectable({ providedIn: 'root' })
export class TextSizeService {
  /** Steps offered by the switcher, in ascending order. */
  readonly available: ReadonlyArray<{ code: TextSize; labelKey: string }> = [
    { code: 'sm', labelKey: 'textSize.small' },
    { code: 'md', labelKey: 'textSize.medium' },
    { code: 'lg', labelKey: 'textSize.large' },
    { code: 'xl', labelKey: 'textSize.xlarge' },
  ];

  /** The currently active text size. */
  readonly size = signal<TextSize>(this.readInitial());

  constructor() {
    this.apply(this.size());
  }

  /** Switch the active text size and persist the choice. */
  use(size: TextSize): void {
    if (!(size in SCALE)) {
      return;
    }
    this.size.set(size);
    try {
      localStorage.setItem(STORAGE_KEY, size);
    } catch {
      /* storage unavailable — non-fatal */
    }
    this.apply(size);
  }

  private apply(size: TextSize): void {
    try {
      document.documentElement.style.setProperty('--ax-root-font', SCALE[size]);
    } catch {
      /* no document (e.g. non-browser test host) — non-fatal */
    }
  }

  private readInitial(): TextSize {
    try {
      const saved = localStorage.getItem(STORAGE_KEY);
      if (saved === 'sm' || saved === 'md' || saved === 'lg' || saved === 'xl') {
        return saved;
      }
    } catch {
      /* storage unavailable — fall through to default */
    }
    return 'md';
  }
}
