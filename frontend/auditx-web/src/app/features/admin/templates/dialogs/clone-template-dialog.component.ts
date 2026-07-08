import {
  ChangeDetectionStrategy,
  Component,
  inject,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import {
  MAT_DIALOG_DATA,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { CloneTemplateRequest } from '../../../../core/models';

export interface CloneTemplateDialogData {
  sourceName: string;
}

@Component({
  selector: 'app-clone-template-dialog',
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
    <h2 mat-dialog-title>{{ 'templatesAdmin.clone.title' | t }}</h2>
    <mat-dialog-content>
      <p class="hint">
        {{ 'templatesAdmin.clone.copiedFrom' | t }}
        <strong>{{ data.sourceName }}</strong>.
      </p>
      <form [formGroup]="form" class="form">
        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ 'templatesAdmin.clone.nameLabel' | t }}</mat-label>
          <input matInput formControlName="newName" autocomplete="off" />
          @if (form.controls.newName.hasError('required') && form.controls.newName.touched) {
            <mat-error>{{ 'templatesAdmin.error.nameRequired' | t }}</mat-error>
          }
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="cancel()">{{ 'templatesAdmin.action.cancel' | t }}</button>
      <button matButton="filled" type="button" (click)="submit()" [disabled]="form.invalid">
        {{ 'templatesAdmin.action.clone' | t }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .form {
      display: flex;
      flex-direction: column;
      min-width: 360px;
    }
    .full {
      width: 100%;
    }
    .hint {
      color: var(--mat-sys-on-surface-variant);
      margin-top: 0;
    }
    @media (max-width: 480px) {
      .form {
        min-width: auto;
      }
    }
  `,
})
export class CloneTemplateDialogComponent {
  readonly data = inject<CloneTemplateDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef =
    inject<MatDialogRef<CloneTemplateDialogComponent, CloneTemplateRequest>>(
      MatDialogRef,
    );
  private readonly fb = inject(FormBuilder);

  readonly form = this.fb.nonNullable.group({
    newName: [
      '',
      [Validators.required, Validators.maxLength(160)],
    ],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.dialogRef.close({ newName: this.form.getRawValue().newName.trim() });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
