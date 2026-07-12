import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { IconComponent } from '../icons/icon.component';
import { MatMenuModule } from '@angular/material/menu';
import { MatTooltipModule } from '@angular/material/tooltip';

import { ThemeService } from './theme.service';
import { TranslatePipe } from '../i18n/translate.pipe';

/** Toolbar control for the appearance theme (Light / Dark / System). */
@Component({
  selector: 'app-theme-toggle',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    MatButtonModule,
    IconComponent,
    MatMenuModule,
    MatTooltipModule,
    TranslatePipe,
  ],
  template: `
    <button
      matIconButton
      type="button"
      [matMenuTriggerFor]="menu"
      [attr.aria-label]="'theme.label' | t"
      [matTooltip]="'theme.label' | t"
    >
      <app-icon [name]="theme.resolved() === 'dark' ? 'dark_mode' : 'light_mode'" />
    </button>
    <mat-menu #menu="matMenu">
      <div class="theme-toggle__heading" mat-menu-item disabled>
        {{ 'theme.label' | t }}
      </div>
      @for (option of theme.available; track option.code) {
        <button mat-menu-item type="button" (click)="theme.use(option.code)">
          <app-icon [name]="theme.preference() === option.code ? 'check' : option.icon" />
          <span>{{ option.labelKey | t }}</span>
        </button>
      }
    </mat-menu>
  `,
  styles: [
    `
      .theme-toggle__heading {
        font-weight: 600;
        opacity: 0.7;
        pointer-events: none;
      }
    `,
  ],
})
export class ThemeToggleComponent {
  readonly theme = inject(ThemeService);
}
