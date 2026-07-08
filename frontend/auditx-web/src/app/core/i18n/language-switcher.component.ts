import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { UpperCasePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatTooltipModule } from '@angular/material/tooltip';

import { TranslationService } from './translation.service';
import { TranslatePipe } from './translate.pipe';

/** Compact language menu (English / Français) for the toolbar and the login screen. */
@Component({
  selector: 'app-language-switcher',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    UpperCasePipe,
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatTooltipModule,
    TranslatePipe,
  ],
  styles: [
    `
      .lang-switch__code {
        font-weight: 600;
        letter-spacing: 0.04em;
      }
    `,
  ],
  template: `
    <button
      matButton
      type="button"
      class="lang-switch"
      [matMenuTriggerFor]="menu"
      [attr.aria-label]="'language.label' | t"
      [matTooltip]="'language.label' | t"
    >
      <mat-icon>language</mat-icon>
      <span class="lang-switch__code">{{ translation.lang() | uppercase }}</span>
      <mat-icon iconPositionEnd>arrow_drop_down</mat-icon>
    </button>
    <mat-menu #menu="matMenu">
      @for (option of translation.available; track option.code) {
        <button mat-menu-item type="button" (click)="translation.use(option.code)">
          <mat-icon>{{ translation.lang() === option.code ? 'check' : 'language' }}</mat-icon>
          <span>{{ option.label }}</span>
        </button>
      }
    </mat-menu>
  `,
})
export class LanguageSwitcherComponent {
  readonly translation = inject(TranslationService);
}
