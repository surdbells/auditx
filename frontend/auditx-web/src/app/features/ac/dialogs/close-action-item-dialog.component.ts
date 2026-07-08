import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import {
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

import { TranslatePipe } from '../../../core/i18n/translate.pipe';

/**
 * CIA closure of an AC action item. A non-blank closure response is required —
 * the backend returns 422 on a blank one, so we enforce it client-side too.
 * Returns the closure response text.
 */
@Component({
  selector: 'app-close-action-item-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    TranslatePipe,
  ],
  template: `
    <h2 mat-dialog-title>{{ 'ac.closeItem.title' | t }}</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="form">
        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ 'ac.closeItem.label' | t }}</mat-label>
          <textarea
            matInput
            formControlName="closureResponse"
            rows="5"
            [placeholder]="'ac.closeItem.placeholder' | t"
          ></textarea>
          @if (form.controls.closureResponse.hasError('required')) {
            <mat-error>{{ 'ac.closeItem.required' | t }}</mat-error>
          }
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="cancel()">{{ 'ac.common.cancel' | t }}</button>
      <button
        matButton="filled"
        type="button"
        (click)="submit()"
        [disabled]="form.invalid"
      >
        {{ 'ac.closeItem.submit' | t }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .form {
      display: flex;
      flex-direction: column;
      min-width: 440px;
    }
    .full {
      width: 100%;
    }
    @media (max-width: 560px) {
      .form {
        min-width: auto;
      }
    }
  `,
})
export class CloseActionItemDialogComponent {
  private readonly dialogRef =
    inject<MatDialogRef<CloseActionItemDialogComponent, string>>(MatDialogRef);
  private readonly fb = inject(FormBuilder);

  readonly form = this.fb.nonNullable.group({
    closureResponse: ['', [Validators.required, Validators.minLength(1)]],
  });

  submit(): void {
    const value = this.form.getRawValue().closureResponse.trim();
    if (!value) {
      this.form.controls.closureResponse.setErrors({ required: true });
      return;
    }
    this.dialogRef.close(value);
  }

  cancel(): void {
    this.dialogRef.close();
  }
}