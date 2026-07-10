import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

import { PageGuide } from '../../../core/models/page-guide.models';
import { TourManagerService } from '../../../core/services/tour-manager.service';
import { TourOverlayComponent } from './tour-overlay.component';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';

/**
 * Drop-in contextual page guide: a "Page guide" button that launches the walkthrough, an "About this page" panel
 * (purpose, what you can do, dependencies, downstream consumers, workflow, permissions, business rules, tips, FAQ +
 * a relationship graph), and the auto-first-run tour (once per device, gated in localStorage). Add it to any page with
 * `<app-page-guide [guide]="GUIDE" />` and a {@link PageGuide} metadata object — everything else is generated.
 */
@Component({
  selector: 'app-page-guide',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatIconModule, TourOverlayComponent, TranslatePipe],
  templateUrl: './page-guide.component.html',
  styleUrl: './page-guide.component.scss',
})
export class PageGuideComponent {
  readonly guide = input.required<PageGuide>();

  protected readonly tour = inject(TourManagerService);

  protected readonly aboutOpen = signal(false);
  protected readonly tourActive = computed(() => this.tour.activeGuideId() === this.guide().id);

  private autoRan = false;
  private autoTimer: ReturnType<typeof setTimeout> | null = null;

  constructor() {
    inject(DestroyRef).onDestroy(() => {
      if (this.autoTimer) {
        clearTimeout(this.autoTimer);
      }
    });

    // Auto-run the tour the first time this page is opened on this device (once the required input has resolved).
    effect(() => {
      const g = this.guide();
      if (this.autoRan) {
        return;
      }
      this.autoRan = true;
      if (!this.tour.hasCompleted(g.id)) {
        this.autoTimer = setTimeout(() => this.tour.start(g), 700);
      }
    });
  }

  startTour(): void {
    this.tour.start(this.guide());
  }

  toggleAbout(): void {
    this.aboutOpen.update((v) => !v);
  }
}
