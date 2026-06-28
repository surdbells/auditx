import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import {
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

import { GenerateAcPackRequest } from '../../../core/models';

/**
 * Generate a new AC pack for a reporting period. HTML is always produced; the
 * checkbox opts a DOCX artefact in. The committee-meeting label is optional.
 */
@Component({
  selector: 'app-generate-pack-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatCheckboxModule,
    MatButtonModule,
  ],
  template: `
    <h2 mat-dialog-title>Generate AC pack</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="form">
        <mat-form-field appearance="outline" class="full">
          <mat-label>Period start</mat-label>
          <input matInput type="date" formControlName="periodStart" />
        </mat-form-field>
        <mat-form-field appearance="outline" class="full">
          <mat-label>Period end</mat-label>
          <input matInput type="date" formControlName="periodEnd" />
        </mat-form-field>
        <mat-form-field appearance="outline" class="full">
          <mat-label>Committee-meeting label (optional)</mat-label>
          <input
            matInput
            formControlName="acMeetingLabel"
            placeholder="e.g. Q1 2026 Audit Committee"
          />
        </mat-form-field>
        <mat-checkbox formControlName="docx">
          Also produce a DOCX artefact
        </mat-checkbox>
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
        Generate
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
    @media (max-width: 560px) {
      .form {
        min-width: auto;
      }
    }
  `,
})
export class GeneratePackDialogComponent {
  private readonly dialogRef =
    inject<MatDialogRef<GeneratePackDialogComponent, GenerateAcPackRequest>>(
      MatDialogRef,
    );
  private readonly fb = inject(FormBuilder);

  readonly form = this.fb.nonNullable.group({
    periodStart: ['', Validators.required],
    periodEnd: ['', Validators.required],
    acMeetingLabel: [''],
    docx: [false],
  });

  submit(): void {
    if (this.form.invalid) {
      return;
    }
    const v = this.form.getRawValue();
    this.dialogRef.close({
      periodStart: v.periodStart,
      periodEnd: v.periodEnd,
      acMeetingLabel: v.acMeetingLabel.trim() || null,
      docx: v.docx,
    });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}