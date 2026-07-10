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

import { TranslatePipe } from '../../../../core/i18n/translate.pipe';

export interface SetCapacityDialogData {
  userDisplayName: string;
  capacityDays: number | null;
}

/** Result: the new capacity in person-days, or null to clear it. */
export interface SetCapacityResult {
  capacityDays: number | null;
}

/** Sets (or clears) a user's annual audit capacity in person-days. */
@Component({
  selector: 'app-set-capacity-dialog',
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
    <h2 mat-dialog-title>{{ 'identity.capacity.title' | t }}</h2>
    <mat-dialog-content>
      <p class="hint">
        {{ 'identity.capacity.hintBefore' | t
        }}<strong>{{ data.userDisplayName }}</strong
        >{{ 'identity.capacity.hintAfter' | t }}
      </p>
      <form [formGroup]="form" class="form">
        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ 'identity.capacity.label' | t }}</mat-label>
          <input
            matInput
            type="number"
            formControlName="capacityDays"
            min="0"
            max="366"
            step="0.5"
          />
          <mat-hint>{{ 'identity.capacity.hint' | t }}</mat-hint>
          @if (form.controls.capacityDays.hasError('min') || form.controls.capacityDays.hasError('max')) {
            <mat-error>{{ 'identity.capacity.rangeError' | t }}</mat-error>
          }
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="cancel()">{{ 'identity.actions.cancel' | t }}</button>
      <button matButton="filled" type="button" (click)="submit()" [disabled]="form.invalid">
        {{ 'identity.capacity.save' | t }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .form {
      display: flex;
      flex-direction: column;
      min-width: 320px;
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
export class SetCapacityDialogComponent {
  readonly data = inject<SetCapacityDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef =
    inject<MatDialogRef<SetCapacityDialogComponent, SetCapacityResult>>(MatDialogRef);
  private readonly fb = inject(FormBuilder);

  readonly form = this.fb.group({
    capacityDays: this.fb.control<number | null>(this.data.capacityDays, [
      Validators.min(0),
      Validators.max(366),
    ]),
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const value = this.form.controls.capacityDays.value;
    // An empty field clears the capacity (null); any number is sent as-is.
    this.dialogRef.close({
      capacityDays: value === null || value === undefined ? null : Number(value),
    });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
