import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
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
    MatButtonModule,
    MatIconModule,
    MatMenuModule,
    MatTooltipModule,
    TranslatePipe,
  ],
  template: `
    <button
      matIconButton
      type="button"
      [matMenuTriggerFor]="menu"
      [attr.aria-label]="'language.label' | t"
      [matTooltip]="'language.label' | t"
    >
      <mat-icon>translate</mat-icon>
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
