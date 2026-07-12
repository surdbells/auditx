import { ChangeDetectionStrategy, Component, computed, input } from '@angular/core';
import { LucideAngularModule } from 'lucide-angular';

import { FALLBACK_ICON, MATERIAL_TO_LUCIDE } from './icon-registry';

/**
 * App-wide icon. Call sites pass the legacy Material Symbols name (e.g. `report_problem`); the
 * component resolves it to a Lucide icon via {@link MATERIAL_TO_LUCIDE} and renders an SVG.
 *
 * Sizing follows font-size: global CSS sets the inner `svg` to `1em`, so any existing icon CSS
 * (`font-size: …`) keeps working exactly as it did for `mat-icon`. Colour is `currentColor`.
 */
@Component({
  selector: 'app-icon',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [LucideAngularModule],
  template: `<lucide-icon [img]="resolved()" aria-hidden="true" />`,
})
export class IconComponent {
  /** Legacy Material Symbols name to render (kept for call-site familiarity). */
  readonly name = input.required<string>();

  protected readonly resolved = computed(
    () => MATERIAL_TO_LUCIDE[this.name()] ?? FALLBACK_ICON,
  );
}
