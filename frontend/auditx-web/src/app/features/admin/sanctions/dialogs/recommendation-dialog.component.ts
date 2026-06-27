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

export interface RecommendationDialogData {
  recommendation: string | null;
  gridRecommendedRange: string | null;
  gridConsultedVersion: number | null;
  deviationReason: string | null;
}

export interface RecommendationDialogResult {
  recommendation: string;
  deviationReason: string | null;
}

/**
 * Captures the sanction recommendation. A deviation reason may always be
 * provided; it is the recommender's record of why the recommendation differs
 * from the grid range (the backend stores it regardless of within-range state).
 */
@Component({
  selector: 'app-recommendation-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
  ],
  template: `
    <h2 mat-dialog-title>Record recommendation</h2>
    <mat-dialog-content>
      @if (data.gridRecommendedRange) {
        <p class="grid-hint">
          Grid v{{ data.gridConsultedVersion }} recommends:
          <strong>{{ data.gridRecommendedRange }}</strong>
        </p>
      } @else {
        <p class="grid-hint muted">
          No active grid cell matched this case — record a recommendation and a
          deviation reason.
        </p>
      }
      <form [formGroup]="form" class="form">
        <mat-form-field appearance="outline" class="full">
          <mat-label>Recommendation</mat-label>
          <textarea matInput formControlName="recommendation" rows="3"></textarea>
          @if (
            form.controls.recommendation.hasError('required') &&
            form.controls.recommendation.touched
          ) {
            <mat-error>A recommendation is required.</mat-error>
          }
        </mat-form-field>
        <mat-form-field appearance="outline" class="full">
          <mat-label>Deviation reason (if outside the grid range)</mat-label>
          <textarea matInput formControlName="deviationReason" rows="2"></textarea>
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="cancel()">Cancel</button>
      <button
        matButton="filled"
        type="button"
        (click)="submit()"
        [disabled]="form.invalid"
      >
        Save
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .form {
      display: flex;
      flex-direction: column;
      min-width: 420px;
    }
    .full {
      width: 100%;
    }
    .grid-hint {
      margin: 0 0 0.75rem;
    }
    .muted {
      color: var(--mat-sys-on-surface-variant);
    }
    @media (max-width: 520px) {
      .form {
        min-width: auto;
      }
    }
  `,
})
export class RecommendationDialogComponent {
  readonly data = inject<RecommendationDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject<
    MatDialogRef<RecommendationDialogComponent, RecommendationDialogResult>
  >(MatDialogRef);
  private readonly fb = inject(FormBuilder);

  readonly form = this.fb.nonNullable.group({
    recommendation: [this.data.recommendation ?? '', [Validators.required]],
    deviationReason: [this.data.deviationReason ?? ''],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    this.dialogRef.close({
      recommendation: v.recommendation.trim(),
      deviationReason: v.deviationReason.trim() || null,
    });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
