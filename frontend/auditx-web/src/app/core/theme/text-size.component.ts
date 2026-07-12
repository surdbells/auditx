import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatMenuModule } from '@angular/material/menu';
import { MatTooltipModule } from '@angular/material/tooltip';

import { TextSizeService } from './text-size.service';
import { TranslatePipe } from '../i18n/translate.pipe';

/** Toolbar control for the page text size (Small → Extra large). */
@Component({
  selector: 'app-text-size',
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
      [attr.aria-label]="'textSize.label' | t"
      [matTooltip]="'textSize.label' | t"
    >
      <mat-icon>format_size</mat-icon>
    </button>
    <mat-menu #menu="matMenu">
      <div class="text-size__heading" mat-menu-item disabled>
        {{ 'textSize.label' | t }}
      </div>
      @for (option of textSize.available; track option.code) {
        <button mat-menu-item type="button" (click)="textSize.use(option.code)">
          <mat-icon>{{ textSize.size() === option.code ? 'check' : 'format_size' }}</mat-icon>
          <span>{{ option.labelKey | t }}</span>
        </button>
      }
    </mat-menu>
  `,
  styles: [
    `
      .text-size__heading {
        font-weight: 600;
        opacity: 0.7;
        pointer-events: none;
      }
    `,
  ],
})
export class TextSizeComponent {
  readonly textSize = inject(TextSizeService);
}
