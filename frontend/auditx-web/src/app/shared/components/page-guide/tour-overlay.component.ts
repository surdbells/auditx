import {
  ChangeDetectionStrategy,
  Component,
  HostListener,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

import { TourManagerService } from '../../../core/services/tour-manager.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';

interface Rect {
  top: number;
  left: number;
  width: number;
  height: number;
}

const SPOT_PADDING = 6;
const GAP = 12;

/**
 * Full-screen walkthrough overlay: dims the page, spotlights the current step's target element (a box-shadow cut-out),
 * and floats a tooltip card near it (or centers it for a modal step). Navigation is Back / Next / Skip / Done with a
 * step counter and dots; keyboard: →/← navigate, Esc skips. Positions recompute on step change, scroll and resize.
 * Theme-aware via `--mat-sys-*` tokens; responsive (the card falls back to a bottom sheet on narrow screens).
 */
@Component({
  selector: 'app-tour-overlay',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatIconModule, TranslatePipe],
  templateUrl: './tour-overlay.component.html',
  styleUrl: './tour-overlay.component.scss',
})
export class TourOverlayComponent {
  protected readonly tour = inject(TourManagerService);

  /** Viewport-relative rect of the highlighted element, or null for a centered modal step. */
  protected readonly spot = signal<Rect | null>(null);

  protected readonly hasSpot = computed(() => this.spot() !== null);

  /** Inline style for the spotlight box. */
  protected readonly spotStyle = computed(() => {
    const r = this.spot();
    if (!r) {
      return null;
    }
    return {
      top: `${r.top - SPOT_PADDING}px`,
      left: `${r.left - SPOT_PADDING}px`,
      width: `${r.width + SPOT_PADDING * 2}px`,
      height: `${r.height + SPOT_PADDING * 2}px`,
    };
  });

  /** Inline style for the tooltip card (anchored below/above the spot, or centered). */
  protected readonly cardStyle = computed<Record<string, string>>(() => {
    const r = this.spot();
    // The step signal is read so this recomputes on navigation even when there is no spot.
    void this.tour.index();
    if (!r || typeof window === 'undefined') {
      return {};
    }
    const cardWidth = Math.min(380, window.innerWidth - 32);
    const below = r.top + r.height + GAP;
    const above = r.top - GAP;
    const placeBelow = below + 220 < window.innerHeight || above < 240;
    let left = r.left + r.width / 2 - cardWidth / 2;
    left = Math.max(16, Math.min(left, window.innerWidth - cardWidth - 16));
    const style: Record<string, string> = { width: `${cardWidth}px`, left: `${left}px` };
    if (placeBelow) {
      style['top'] = `${below}px`;
    } else {
      style['bottom'] = `${window.innerHeight - above}px`;
    }
    return style;
  });

  constructor() {
    // Re-locate the target whenever the step changes.
    effect(() => {
      this.tour.index();
      this.tour.activeGuideId();
      queueMicrotask(() => this.locate());
    });
  }

  @HostListener('window:resize')
  @HostListener('window:scroll')
  onViewportChange(): void {
    this.locate();
  }

  @HostListener('document:keydown', ['$event'])
  onKey(event: KeyboardEvent): void {
    if (!this.tour.activeGuideId()) {
      return;
    }
    if (event.key === 'Escape') {
      this.tour.skip();
    } else if (event.key === 'ArrowRight') {
      this.tour.next();
    } else if (event.key === 'ArrowLeft') {
      this.tour.previous();
    }
  }

  private locate(): void {
    const step = this.tour.currentStep();
    if (!step?.selector) {
      this.spot.set(null);
      return;
    }
    const el = document.querySelector(step.selector);
    if (!el) {
      this.spot.set(null);
      return;
    }
    el.scrollIntoView({ block: 'center', behavior: 'smooth' });
    const r = el.getBoundingClientRect();
    this.spot.set({ top: r.top, left: r.left, width: r.width, height: r.height });
  }
}
