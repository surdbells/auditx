import { DOCUMENT } from '@angular/common';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';

import { ApiService } from './api.service';
import { Branding } from '../models';

/** Fallback theme colours (must mirror styles.scss / tailwind.css defaults). */
const DEFAULT_PRIMARY = '#4f46e5';
const DEFAULT_ACCENT = '#7c3aed';
const DEFAULT_NAME = 'AuditX';

/**
 * Loads the organisation branding (name, theme colours, logo, icon) and applies it to the running app:
 * overriding the Material `--mat-sys-*` and Tailwind `--color-*` custom properties on the document root,
 * swapping the favicon, and exposing the name + logo as signals the shell binds to.
 *
 * Fetched once anonymously at bootstrap (so the login screen is themed) and re-applied in place whenever
 * an admin saves the settings form — no reload required.
 */
@Injectable({ providedIn: 'root' })
export class BrandingService {
  private readonly api = inject(ApiService);
  private readonly document = inject(DOCUMENT);

  readonly organizationName = signal(DEFAULT_NAME);
  readonly logoDataUri = signal<string | null>(null);

  /** Fetches `/branding` and applies it. Never throws for callers that ignore the result. */
  load(): Observable<Branding> {
    return this.api.get<Branding>('/branding').pipe(tap((b) => this.apply(b)));
  }

  /** Applies branding to the DOM in place (used at bootstrap and after an in-app settings save). */
  apply(branding: Branding): void {
    this.organizationName.set(branding.organizationName?.trim() || DEFAULT_NAME);
    this.logoDataUri.set(branding.logoDataUri || null);

    const root = this.document.documentElement;
    this.applyColor(root, branding.primaryColor || DEFAULT_PRIMARY, 'primary', 'brand');
    this.applyColor(root, branding.accentColor || DEFAULT_ACCENT, 'tertiary', 'accent');
    this.applyFavicon(branding.iconDataUri);
  }

  private applyColor(
    root: HTMLElement,
    hex: string,
    matToken: 'primary' | 'tertiary',
    tailwindRamp: 'brand' | 'accent',
  ): void {
    root.style.setProperty(`--mat-sys-${matToken}`, hex);
    root.style.setProperty(`--mat-sys-on-${matToken}`, this.contrastOn(hex));
    // Recolour the mid + strong steps the gradients/buttons pull from.
    root.style.setProperty(`--color-${tailwindRamp}-500`, hex);
    root.style.setProperty(`--color-${tailwindRamp}-600`, hex);
  }

  /** Swaps (or creates) the favicon link when a custom icon is supplied; leaves the default otherwise. */
  private applyFavicon(iconDataUri: string | null): void {
    if (!iconDataUri) {
      return;
    }
    let link = this.document.querySelector<HTMLLinkElement>('link[rel="icon"]');
    if (!link) {
      link = this.document.createElement('link');
      link.rel = 'icon';
      this.document.head.appendChild(link);
    }
    link.href = iconDataUri;
  }

  /** Returns near-black or white for legible text/icons on the given background colour (WCAG luminance). */
  private contrastOn(hex: string): string {
    const m = /^#?([0-9a-f]{6})$/i.exec(hex.trim());
    if (!m) {
      return '#ffffff';
    }
    const int = parseInt(m[1], 16);
    const channels = [(int >> 16) & 255, (int >> 8) & 255, int & 255].map((c) => {
      const s = c / 255;
      return s <= 0.03928 ? s / 12.92 : ((s + 0.055) / 1.055) ** 2.4;
    });
    const luminance = 0.2126 * channels[0] + 0.7152 * channels[1] + 0.0722 * channels[2];
    return luminance > 0.4 ? '#0f172a' : '#ffffff';
  }
}
