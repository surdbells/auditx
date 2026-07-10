import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import {
  MAT_DIALOG_DATA,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';

import { TranslatePipe } from '../../../core/i18n/translate.pipe';

export interface SaveViewDialogData {
  /** Prefilled name + shared flag when editing an existing view; blank for a new one. */
  name?: string;
  isShared?: boolean;
  /** Title shown on the dialog (already translated). */
  title: string;
}

export interface SaveViewResult {
  name: string;
  isShared: boolean;
}

/** Captures a name + shared flag for saving the current filter selection as a view (D3-A). */
@Component({
  selector: 'app-save-view-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSlideToggleModule,
    MatButtonModule,
    TranslatePipe,
  ],
  template: `
    <h2 mat-dialog-title>{{ data.title }}</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="sv-form">
        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ 'savedViews.field.name' | t }}</mat-label>
          <input matInput formControlName="name" maxlength="120" autocomplete="off" cdkFocusInitial />
          @if (form.controls.name.hasError('required') && form.controls.name.touched) {
            <mat-error>{{ 'savedViews.error.nameRequired' | t }}</mat-error>
          }
        </mat-form-field>
        <mat-slide-toggle formControlName="isShared">{{ 'savedViews.field.shared' | t }}</mat-slide-toggle>
        <p class="sv-hint">{{ 'savedViews.field.sharedHint' | t }}</p>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="cancel()">{{ 'savedViews.actions.cancel' | t }}</button>
      <button matButton="filled" type="button" (click)="submit()" [disabled]="form.invalid">
        {{ 'savedViews.actions.save' | t }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .sv-form {
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
      min-width: 380px;
    }
    .full {
      width: 100%;
    }
    .sv-hint {
      margin: 0.25rem 0 0;
      color: var(--mat-sys-on-surface-variant);
      font: var(--mat-sys-body-small);
    }
    @media (max-width: 520px) {
      .sv-form {
        min-width: auto;
      }
    }
  `,
})
export class SaveViewDialogComponent {
  readonly data = inject<SaveViewDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject<MatDialogRef<SaveViewDialogComponent, SaveViewResult>>(MatDialogRef);
  private readonly fb = inject(FormBuilder);

  readonly form = this.fb.nonNullable.group({
    name: [this.data.name ?? '', [Validators.required, Validators.maxLength(120)]],
    isShared: [this.data.isShared ?? false],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const { name, isShared } = this.form.getRawValue();
    this.dialogRef.close({ name: name.trim(), isShared });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
