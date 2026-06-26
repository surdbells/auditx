import {
  ChangeDetectionStrategy,
  Component,
  inject,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import {
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatRadioModule } from '@angular/material/radio';

import { PlanDecisionKind, PlanDecisionRequest } from '../../../core/models';

/** AC Chair decision dialog for a submitted plan. */
@Component({
  selector: 'app-plan-decision-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatRadioModule,
    MatButtonModule,
  ],
  template: `
    <h2 mat-dialog-title>Plan decision</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="form">
        <mat-radio-group formControlName="decision" class="decision">
          <mat-radio-button value="approved">Approve</mat-radio-button>
          <mat-radio-button value="revisions_requested">
            Request revisions
          </mat-radio-button>
          <mat-radio-button value="rejected">Reject</mat-radio-button>
        </mat-radio-group>

        <mat-form-field appearance="outline" class="full">
          <mat-label>Detail</mat-label>
          <input matInput formControlName="detail" autocomplete="off" />
        </mat-form-field>

        <mat-form-field appearance="outline" class="full">
          <mat-label>Comments</mat-label>
          <textarea matInput formControlName="comments" rows="3"></textarea>
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="cancel()">Cancel</button>
      <button matButton="filled" type="button" (click)="submit()" [disabled]="form.invalid">
        Submit decision
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .form {
      display: flex;
      flex-direction: column;
      min-width: 420px;
      gap: 0.5rem;
    }
    .decision {
      display: flex;
      flex-direction: column;
      margin-bottom: 0.5rem;
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
export class PlanDecisionDialogComponent {
  private readonly dialogRef =
    inject<MatDialogRef<PlanDecisionDialogComponent, PlanDecisionRequest>>(
      MatDialogRef,
    );
  private readonly fb = inject(FormBuilder);

  readonly form = this.fb.nonNullable.group({
    decision: ['approved' as PlanDecisionKind, [Validators.required]],
    detail: [''],
    comments: [''],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    this.dialogRef.close({
      decision: v.decision,
      detail: v.detail.trim() || undefined,
      comments: v.comments.trim() || undefined,
    });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
