import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import {
  MAT_DIALOG_DATA,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';

import { UserDto } from '../../../core/models';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';

export interface ReassignOwnerDialogData {
  currentOwnerId: string;
  users: UserDto[];
}

export interface ReassignOwnerDialogResult {
  ownerUserId: string;
}

@Component({
  selector: 'app-reassign-owner-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatSelectModule,
    MatButtonModule,
    TranslatePipe,
  ],
  template: `
    <h2 mat-dialog-title>{{ 'exceptions.dialog.reassignTitle' | t }}</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="form">
        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ 'exceptions.field.owner' | t }}</mat-label>
          <mat-select formControlName="ownerUserId">
            @for (u of data.users; track u.id) {
              <mat-option [value]="u.id">{{ u.displayName }}</mat-option>
            }
          </mat-select>
          @if (form.controls.ownerUserId.hasError('required') && form.controls.ownerUserId.touched) {
            <mat-error>{{ 'exceptions.error.selectOwner' | t }}</mat-error>
          }
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="cancel()">{{ 'exceptions.action.cancel' | t }}</button>
      <button matButton="filled" type="button" (click)="submit()" [disabled]="form.invalid">
        {{ 'exceptions.action.save' | t }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .form {
      display: flex;
      flex-direction: column;
      min-width: 400px;
    }
    .full {
      width: 100%;
    }
    @media (max-width: 520px) {
      .form {
        min-width: auto;
      }
    }
  `,
})
export class ReassignOwnerDialogComponent {
  readonly data = inject<ReassignOwnerDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject<
    MatDialogRef<ReassignOwnerDialogComponent, ReassignOwnerDialogResult>
  >(MatDialogRef);
  private readonly fb = inject(FormBuilder);

  readonly form = this.fb.nonNullable.group({
    ownerUserId: [this.data.currentOwnerId, [Validators.required]],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.dialogRef.close({ ownerUserId: this.form.getRawValue().ownerUserId });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
