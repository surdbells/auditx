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
import { MatSlideToggleModule } from '@angular/material/slide-toggle';

import {
  CreateRiskDimensionRequest,
  RiskDimension,
  UpdateRiskDimensionRequest,
} from '../../../core/models';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';

export interface RiskDimensionDialogData {
  /** Present when editing; absent for create. */
  dimension?: RiskDimension;
}

export type RiskDimensionDialogResult =
  | { mode: 'create'; body: CreateRiskDimensionRequest }
  | { mode: 'edit'; id: string; body: UpdateRiskDimensionRequest };

@Component({
  selector: 'app-risk-dimension-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatSlideToggleModule,
    TranslatePipe,
  ],
  template: `
    <h2 mat-dialog-title>
      {{ (isEdit ? 'universe.dimDialog.editTitle' : 'universe.dimDialog.newTitle') | t }}
    </h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="form">
        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ 'universe.field.name' | t }}</mat-label>
          <input matInput formControlName="name" autocomplete="off" />
          @if (form.controls.name.hasError('required') && form.controls.name.touched) {
            <mat-error>{{ 'universe.error.nameRequired' | t }}</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline">
          <mat-label>{{ 'universe.field.weight' | t }}</mat-label>
          <input matInput type="number" formControlName="weight" min="0" />
        </mat-form-field>

        <div class="row">
          <mat-form-field appearance="outline">
            <mat-label>{{ 'universe.field.scaleMin' | t }}</mat-label>
            <input matInput type="number" formControlName="scaleMin" />
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>{{ 'universe.field.scaleMax' | t }}</mat-label>
            <input matInput type="number" formControlName="scaleMax" />
          </mat-form-field>
        </div>

        @if (isEdit) {
          <mat-slide-toggle formControlName="isActive">{{ 'universe.filter.active' | t }}</mat-slide-toggle>
        }
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="cancel()">{{ 'universe.actions.cancel' | t }}</button>
      <button
        matButton="filled"
        type="button"
        (click)="submit()"
        [disabled]="form.invalid"
      >
        {{ (isEdit ? 'universe.actions.save' : 'universe.actions.create') | t }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .form {
      display: flex;
      flex-direction: column;
      min-width: 360px;
      gap: 0.25rem;
    }
    .row {
      display: flex;
      gap: 1rem;
    }
    .row mat-form-field {
      flex: 1;
    }
    .full {
      width: 100%;
    }
    @media (max-width: 480px) {
      .form {
        min-width: auto;
      }
    }
  `,
})
export class RiskDimensionDialogComponent {
  readonly data = inject<RiskDimensionDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef =
    inject<MatDialogRef<RiskDimensionDialogComponent, RiskDimensionDialogResult>>(
      MatDialogRef,
    );
  private readonly fb = inject(FormBuilder);

  readonly isEdit = !!this.data.dimension;

  readonly form = this.fb.nonNullable.group({
    name: [
      { value: this.data.dimension?.name ?? '', disabled: this.isEdit },
      [Validators.required, Validators.maxLength(120)],
    ],
    weight: [this.data.dimension?.weight ?? 1, [Validators.required]],
    scaleMin: [this.data.dimension?.scaleMin ?? 1],
    scaleMax: [this.data.dimension?.scaleMax ?? 5],
    isActive: [this.data.dimension?.isActive ?? true],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    if (this.isEdit && this.data.dimension) {
      this.dialogRef.close({
        mode: 'edit',
        id: this.data.dimension.id,
        body: {
          weight: Number(v.weight),
          scaleMin: Number(v.scaleMin),
          scaleMax: Number(v.scaleMax),
          isActive: v.isActive,
        },
      });
    } else {
      this.dialogRef.close({
        mode: 'create',
        body: {
          name: v.name.trim(),
          weight: Number(v.weight),
          scaleMin: Number(v.scaleMin),
          scaleMax: Number(v.scaleMax),
        },
      });
    }
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
