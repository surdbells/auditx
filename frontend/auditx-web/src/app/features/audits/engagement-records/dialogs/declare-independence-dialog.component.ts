import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatRadioModule } from '@angular/material/radio';

import { TranslatePipe } from '../../../../core/i18n/translate.pipe';

/** The server's limit for conflict details and for a manager's safeguards. */
export const INDEPENDENCE_TEXT_MAX_LENGTH = 2000;

export interface DeclareIndependenceResult {
  hasConflict: boolean;
  conflictDetails: string | null;
}

/**
 * The signed-in team member's independence declaration: independent of the area, or a conflict of interest described
 * for a manager to decide on. Closes with the declaration, or nothing when cancelled.
 */
@Component({
  selector: 'app-declare-independence-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatRadioModule, MatButtonModule, TranslatePipe],
  template: `
    <h2 mat-dialog-title>{{ 'engagement.independence.dialog.title' | t }}</h2>
    <mat-dialog-content>
      <p class="hint">{{ 'engagement.independence.dialog.hint' | t }}</p>
      <mat-radio-group class="choices" [value]="conflict() ? 'conflict' : 'independent'" (change)="choose($event.value)">
        <mat-radio-button value="independent">{{ 'engagement.independence.dialog.independent' | t }}</mat-radio-button>
        <mat-radio-button value="conflict">{{ 'engagement.independence.dialog.conflict' | t }}</mat-radio-button>
      </mat-radio-group>
      @if (conflict()) {
        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ 'engagement.independence.dialog.detailsLabel' | t }}</mat-label>
          <textarea matInput [formControl]="details" rows="4" cdkFocusInitial></textarea>
          <mat-hint>{{ 'engagement.independence.dialog.detailsHint' | t }}</mat-hint>
          @if ((details.hasError('required') || details.hasError('pattern')) && details.touched) {
            <mat-error>{{ 'engagement.independence.dialog.detailsRequired' | t }}</mat-error>
          } @else if (details.hasError('maxlength')) {
            <mat-error>{{ 'engagement.dialog.tooLong' | t: { max: max } }}</mat-error>
          }
        </mat-form-field>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" mat-dialog-close>{{ 'common.cancel' | t }}</button>
      <button matButton="filled" type="button" (click)="submit()" [disabled]="conflict() && details.invalid">
        {{ 'engagement.independence.dialog.submit' | t }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .hint {
      margin: 0 0 12px;
      color: var(--mat-sys-on-surface-variant);
      font-size: 0.875rem;
      line-height: 1.45;
    }
    .choices {
      display: flex;
      flex-direction: column;
      gap: 6px;
      margin-bottom: 12px;
    }
    .full {
      width: 100%;
    }
  `,
})
export class DeclareIndependenceDialogComponent {
  private readonly dialogRef = inject<MatDialogRef<DeclareIndependenceDialogComponent, DeclareIndependenceResult>>(MatDialogRef);

  readonly max = INDEPENDENCE_TEXT_MAX_LENGTH;
  readonly conflict = signal(false);
  readonly details = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.pattern(/\S/), Validators.maxLength(INDEPENDENCE_TEXT_MAX_LENGTH)],
  });

  choose(value: 'independent' | 'conflict'): void {
    this.conflict.set(value === 'conflict');
  }

  submit(): void {
    if (this.conflict() && this.details.invalid) {
      this.details.markAsTouched();
      return;
    }
    this.dialogRef.close(
      this.conflict() ? { hasConflict: true, conflictDetails: this.details.value.trim() } : { hasConflict: false, conflictDetails: null },
    );
  }
}
