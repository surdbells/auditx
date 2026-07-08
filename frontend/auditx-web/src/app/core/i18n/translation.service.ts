import { Injectable, signal } from '@angular/core';

import EN from './en.json';
import FR from './fr.json';

/** Supported UI languages. */
export type AppLang = 'en' | 'fr';

const STORAGE_KEY = 'auditx.lang';

const DICTIONARIES: Record<AppLang, Record<string, string>> = {
  en: EN as Record<string, string>,
  fr: FR as Record<string, string>,
};

/**
 * Lightweight, dependency-free runtime i18n. Dictionaries are bundled (imported
 * statically) so translation is synchronous — no HTTP load, no flash of untranslated
 * content, and specs resolve English keys without any test wiring. The active language
 * is a signal; the {@link TranslatePipe} re-renders views when it changes.
 */
@Injectable({ providedIn: 'root' })
export class TranslationService {
  /** Selectable languages for the switcher. */
  readonly available: ReadonlyArray<{ code: AppLang; label: string }> = [
    { code: 'en', label: 'English' },
    { code: 'fr', label: 'Français' },
  ];

  /** The currently active language. */
  readonly lang = signal<AppLang>(this.readInitial());

  constructor() {
    this.applyDocumentLang(this.lang());
  }

  /** Switch the active language and persist the choice. */
  use(lang: AppLang): void {
    if (lang !== 'en' && lang !== 'fr') {
      return;
    }
    this.lang.set(lang);
    try {
      localStorage.setItem(STORAGE_KEY, lang);
    } catch {
      /* storage unavailable — non-fatal */
    }
    this.applyDocumentLang(lang);
  }

  /**
   * Resolve a translation key for the active language. Falls back to English, then to
   * the raw key. `{{name}}`-style placeholders are replaced from `params`.
   */
  translate(key: string, params?: Record<string, string | number>): string {
    const active = DICTIONARIES[this.lang()] ?? DICTIONARIES.en;
    let value = active[key] ?? DICTIONARIES.en[key] ?? key;
    if (params) {
      for (const [name, replacement] of Object.entries(params)) {
        value = value.split(`{{${name}}}`).join(String(replacement));
      }
    }
    return value;
  }

  private readInitial(): AppLang {
    try {
      const saved = localStorage.getItem(STORAGE_KEY);
      if (saved === 'en' || saved === 'fr') {
        return saved;
      }
    } catch {
      /* storage unavailable — fall through to default */
    }
    return 'en';
  }

  private applyDocumentLang(lang: AppLang): void {
    try {
      document.documentElement.lang = lang;
    } catch {
      /* no document (e.g. non-browser test host) — non-fatal */
    }
  }
}
