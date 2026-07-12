import { Injectable, computed, effect, signal } from '@angular/core';

/** User appearance preference. `system` follows the OS `prefers-color-scheme`. */
export type ThemePref = 'light' | 'dark' | 'system';
/** The concrete theme actually applied to the document. */
export type ResolvedTheme = 'light' | 'dark';

const STORAGE_KEY = 'auditx.theme';

/**
 * Persists the user's appearance preference and applies the resolved theme by stamping
 * `data-theme` on the document root (styles.scss redefines the `--mat-sys-*` tokens under
 * `html[data-theme='dark']`). Mirrors {@link TranslationService}'s signal + localStorage shape;
 * `system` tracks the OS scheme live via `matchMedia`.
 */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  /** Options for the switcher, in display order. */
  readonly available: ReadonlyArray<{ code: ThemePref; labelKey: string; icon: string }> = [
    { code: 'light', labelKey: 'theme.light', icon: 'light_mode' },
    { code: 'dark', labelKey: 'theme.dark', icon: 'dark_mode' },
    { code: 'system', labelKey: 'theme.system', icon: 'brightness_auto' },
  ];

  /** The stored preference (light / dark / system). */
  readonly preference = signal<ThemePref>(this.readInitial());

  private readonly systemDark = signal(this.querySystemDark());

  /** The concrete theme after resolving `system` against the OS. */
  readonly resolved = computed<ResolvedTheme>(() => {
    const pref = this.preference();
    if (pref === 'system') {
      return this.systemDark() ? 'dark' : 'light';
    }
    return pref;
  });

  constructor() {
    // Track OS scheme changes so `system` flips live.
    try {
      const mq = window.matchMedia('(prefers-color-scheme: dark)');
      mq.addEventListener('change', (e) => this.systemDark.set(e.matches));
    } catch {
      /* matchMedia unavailable — non-fatal */
    }
    // Apply whenever the resolved theme changes.
    effect(() => this.apply(this.resolved()));
  }

  /** Set and persist the appearance preference. */
  use(pref: ThemePref): void {
    if (pref !== 'light' && pref !== 'dark' && pref !== 'system') {
      return;
    }
    this.preference.set(pref);
    try {
      localStorage.setItem(STORAGE_KEY, pref);
    } catch {
      /* storage unavailable — non-fatal */
    }
  }

  private apply(theme: ResolvedTheme): void {
    try {
      document.documentElement.setAttribute('data-theme', theme);
    } catch {
      /* no document (e.g. non-browser test host) — non-fatal */
    }
  }

  private querySystemDark(): boolean {
    try {
      return window.matchMedia('(prefers-color-scheme: dark)').matches;
    } catch {
      return false;
    }
  }

  private readInitial(): ThemePref {
    try {
      const saved = localStorage.getItem(STORAGE_KEY);
      if (saved === 'light' || saved === 'dark' || saved === 'system') {
        return saved;
      }
    } catch {
      /* storage unavailable — fall through to default */
    }
    return 'light';
  }
}
