import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import {
  MAT_DIALOG_DATA,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';

import { TranslationService } from '../../../core/i18n/translation.service';

export interface ConfirmDialogData {
  title: string;
  message: string;
  confirmLabel?: string;
  cancelLabel?: string;
  /** When true, the confirm button is styled as a destructive (warn) action. */
  destructive?: boolean;
}

@Component({
  selector: 'app-confirm-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [MatButtonModule, MatDialogModule],
  template: `
    <h2 mat-dialog-title>{{ data.title }}</h2>
    <mat-dialog-content>
      <p>{{ data.message }}</p>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="cancel()">
        {{ data.cancelLabel ?? cancelFallback }}
      </button>
      <button
        [matButton]="data.destructive ? 'filled' : 'filled'"
        type="button"
        [class.destructive]="data.destructive"
        (click)="confirm()"
        cdkFocusInitial
      >
        {{ data.confirmLabel ?? confirmFallback }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .destructive {
      --mat-filled-button-container-color: var(--mat-sys-error);
      --mat-filled-button-label-text-color: var(--mat-sys-on-error);
    }
  `,
})
export class ConfirmDialogComponent {
  readonly data = inject<ConfirmDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef =
    inject<MatDialogRef<ConfirmDialogComponent, boolean>>(MatDialogRef);
  private readonly i18n = inject(TranslationService);

  /** Localised fallback for the cancel button when the caller supplies no label. */
  readonly cancelFallback = this.i18n.translate('shared.dialog.cancel');
  /** Localised fallback for the confirm button when the caller supplies no label. */
  readonly confirmFallback = this.i18n.translate('shared.dialog.confirm');

  confirm(): void {
    this.dialogRef.close(true);
  }

  cancel(): void {
    this.dialogRef.close(false);
  }
}
