import { Injectable, computed, signal } from '@angular/core';

/**
 * The UTC offset for an IANA zone at a given instant, formatted the way Angular's DatePipe expects its `timezone`
 * argument (e.g. "+0100", or "UTC" for zero). Uses the platform Intl database, so it is DST-aware for the current
 * offset. (Angular's DatePipe only accepts an offset, not an IANA name, so we resolve the offset here.)
 */
export function zoneOffset(zone: string, at: Date = new Date()): string {
  try {
    const name =
      new Intl.DateTimeFormat('en-US', { timeZone: zone, timeZoneName: 'longOffset' })
        .formatToParts(at)
        .find((p) => p.type === 'timeZoneName')?.value ?? 'GMT';
    const m = /GMT([+-])(\d{1,2})(?::?(\d{2}))?/.exec(name);
    if (!m) {
      return 'UTC';
    }
    return `${m[1]}${m[2].padStart(2, '0')}${(m[3] ?? '00').padStart(2, '0')}`;
  } catch {
    return 'UTC';
  }
}

/**
 * Holds the effective display timezone/locale for the signed-in user. It defaults to the browser-detected IANA zone
 * (so timestamps render in the viewer's local zone from the first paint, with no server round-trip), and can be
 * overridden by the user's stored preference. The offset it exposes feeds the global DATE_PIPE_DEFAULT_OPTIONS so
 * every `| date` in the app converts to this zone.
 */
@Injectable({ providedIn: 'root' })
export class TimezoneService {
  /** What the viewer's browser reports — the user's actual local zone. */
  readonly browserZone = safeBrowserZone();

  private readonly _zone = signal<string>(this.browserZone);
  private readonly _locale = signal<string>(navigator.language || 'en-GB');

  /** The IANA zone timestamps are rendered in. */
  readonly zone = this._zone.asReadonly();
  readonly locale = this._locale.asReadonly();

  /** The current offset for {@link zone}, as Angular's DatePipe timezone argument. */
  readonly offset = computed(() => zoneOffset(this._zone()));

  setZone(zone: string | null | undefined): void {
    if (zone && zone.trim()) {
      this._zone.set(zone.trim());
    }
  }

  setLocale(locale: string | null | undefined): void {
    if (locale && locale.trim()) {
      this._locale.set(locale.trim());
    }
  }
}

function safeBrowserZone(): string {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC';
  } catch {
    return 'UTC';
  }
}
