import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import {
  FormBuilder,
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import {
  MAT_DIALOG_DATA,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';

import { IconComponent } from '../../../../core/icons/icon.component';
import {
  CreateRatingScaleRequest,
  RatingScale,
  RatingScalePoint,
  UpdateRatingScaleRequest,
} from '../../../../core/models';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';

export interface RatingScaleDialogData {
  /** Present when editing; absent for create. */
  scale?: RatingScale;
}

export type RatingScaleDialogResult =
  | { mode: 'create'; body: CreateRatingScaleRequest }
  | { mode: 'edit'; id: string; body: UpdateRatingScaleRequest };

type PointGroup = FormGroup<{
  value: FormControl<number>;
  label: FormControl<string>;
  score: FormControl<number>;
}>;

function parsePoints(pointsJson: string | undefined): RatingScalePoint[] {
  if (!pointsJson) {
    return [
      { value: 1, label: '', score: 0 },
      { value: 2, label: '', score: 100 },
    ];
  }
  try {
    const parsed = JSON.parse(pointsJson);
    return Array.isArray(parsed) ? parsed : [];
  } catch {
    return [];
  }
}

@Component({
  selector: 'app-rating-scale-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatSlideToggleModule,
    IconComponent,
    TranslatePipe,
  ],
  template: `
    <h2 mat-dialog-title>
      {{ (isEdit ? 'templatesAdmin.ratingScaleDialog.editTitle' : 'templatesAdmin.ratingScaleDialog.newTitle') | t }}
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

        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ 'templatesAdmin.ratingScaleDialog.description' | t }}</mat-label>
          <textarea matInput formControlName="description" rows="2"></textarea>
        </mat-form-field>

        @if (isEdit) {
          <mat-slide-toggle formControlName="isActive">{{ 'universe.filter.active' | t }}</mat-slide-toggle>
        }

        <div class="points">
          <div class="points__head">
            <span class="points__label">{{ 'templatesAdmin.ratingScaleDialog.points' | t }}</span>
            <button matButton type="button" (click)="addPoint()">
              <app-icon name="add" />
              {{ 'templatesAdmin.ratingScaleDialog.addPoint' | t }}
            </button>
          </div>

          @for (point of points.controls; track $index) {
            <div class="points__row" [formGroup]="point">
              <mat-form-field appearance="outline" class="points__value">
                <mat-label>{{ 'templatesAdmin.ratingScaleDialog.pointValue' | t }}</mat-label>
                <input matInput type="number" formControlName="value" />
              </mat-form-field>
              <mat-form-field appearance="outline" class="points__grow">
                <mat-label>{{ 'templatesAdmin.ratingScaleDialog.pointLabel' | t }}</mat-label>
                <input matInput formControlName="label" autocomplete="off" />
              </mat-form-field>
              <mat-form-field appearance="outline" class="points__value">
                <mat-label>{{ 'templatesAdmin.ratingScaleDialog.pointScore' | t }}</mat-label>
                <input matInput type="number" min="0" max="100" formControlName="score" />
              </mat-form-field>
              <button
                matIconButton
                type="button"
                [disabled]="points.length <= 2"
                (click)="removePoint($index)"
                [attr.aria-label]="'templatesAdmin.ratingScaleDialog.removePoint' | t"
              >
                <app-icon name="delete" />
              </button>
            </div>
          }
          @if (pointsError()) {
            <p class="points__error">{{ pointsError() }}</p>
          }
        </div>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="cancel()">{{ 'universe.actions.cancel' | t }}</button>
      <button matButton="filled" type="button" (click)="submit()" [disabled]="form.invalid">
        {{ (isEdit ? 'universe.actions.save' : 'universe.actions.create') | t }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .form {
      display: flex;
      flex-direction: column;
      min-width: 480px;
      gap: 0.25rem;
    }
    .full {
      width: 100%;
    }
    .points {
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
      margin-top: 0.5rem;
    }
    .points__head {
      display: flex;
      align-items: center;
      justify-content: space-between;
    }
    .points__label {
      font-weight: 500;
      font-size: 0.875rem;
    }
    .points__row {
      display: flex;
      align-items: flex-start;
      gap: 0.5rem;
    }
    .points__value {
      width: 6rem;
    }
    .points__grow {
      flex: 1;
    }
    .points__error {
      color: var(--mat-sys-error, #b3261e);
      font-size: 0.8125rem;
      margin: 0;
    }
    @media (max-width: 560px) {
      .form {
        min-width: auto;
      }
    }
  `,
})
export class RatingScaleDialogComponent {
  readonly data = inject<RatingScaleDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef =
    inject<MatDialogRef<RatingScaleDialogComponent, RatingScaleDialogResult>>(
      MatDialogRef,
    );
  private readonly fb = inject(FormBuilder);

  readonly isEdit = !!this.data.scale;

  readonly form = this.fb.nonNullable.group({
    name: [
      { value: this.data.scale?.name ?? '', disabled: this.isEdit },
      [Validators.required, Validators.maxLength(100)],
    ],
    description: [this.data.scale?.description ?? ''],
    isActive: [this.data.scale?.isActive ?? true],
    points: this.fb.nonNullable.array<PointGroup>(
      parsePoints(this.data.scale?.pointsJson).map((p) => this.pointGroup(p)),
    ),
  });

  get points() {
    return this.form.controls.points;
  }

  private pointGroup(point: RatingScalePoint): PointGroup {
    return this.fb.nonNullable.group({
      value: this.fb.nonNullable.control(point.value, [Validators.required]),
      label: this.fb.nonNullable.control(point.label, [Validators.required]),
      score: this.fb.nonNullable.control(point.score, [
        Validators.required,
        Validators.min(0),
        Validators.max(100),
      ]),
    });
  }

  addPoint(): void {
    const next = this.points.length + 1;
    this.points.push(this.pointGroup({ value: next, label: '', score: 0 }));
  }

  removePoint(index: number): void {
    if (this.points.length > 2) {
      this.points.removeAt(index);
    }
  }

  pointsError(): string | null {
    const raw = this.points.getRawValue();
    const values = raw.map((p) => Number(p.value));
    if (new Set(values).size !== values.length) {
      return 'Point values must be unique.';
    }
    return null;
  }

  submit(): void {
    if (this.form.invalid || this.pointsError()) {
      this.form.markAllAsTouched();
      return;
    }

    const v = this.form.getRawValue();
    const pointsJson = JSON.stringify(
      v.points.map((p) => ({
        value: Number(p.value),
        label: p.label.trim(),
        score: Number(p.score),
      })),
    );

    if (this.isEdit && this.data.scale) {
      this.dialogRef.close({
        mode: 'edit',
        id: this.data.scale.id,
        body: {
          description: v.description.trim() || null,
          isActive: v.isActive,
          pointsJson,
        },
      });
    } else {
      this.dialogRef.close({
        mode: 'create',
        body: {
          name: v.name.trim(),
          description: v.description.trim() || null,
          pointsJson,
        },
      });
    }
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
