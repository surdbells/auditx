import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import {
  MAT_DIALOG_DATA,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';

import { AuditTeamMember } from '../../../core/models';

export interface TransferLeadCandidate {
  member: AuditTeamMember;
  displayName: string;
}

export interface TransferLeadDialogData {
  /** Active auditors / reviewers eligible to become the new lead. */
  candidates: TransferLeadCandidate[];
}

export interface TransferLeadDialogResult {
  newLeadUserId: string;
  removeOutgoing: boolean;
}

@Component({
  selector: 'app-transfer-lead-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatSelectModule,
    MatCheckboxModule,
    MatButtonModule,
  ],
  template: `
    <h2 mat-dialog-title>Transfer lead</h2>
    <mat-dialog-content>
      @if (!data.candidates.length) {
        <p class="muted">
          No active auditors or reviewers are available to take over as lead.
        </p>
      } @else {
        <form [formGroup]="form" class="form">
          <mat-form-field appearance="outline" class="full">
            <mat-label>New lead</mat-label>
            <mat-select formControlName="newLeadUserId">
              @for (c of data.candidates; track c.member.id) {
                <mat-option [value]="c.member.userId">
                  {{ c.displayName }} ({{ c.member.teamRole }})
                </mat-option>
              }
            </mat-select>
            @if (form.controls.newLeadUserId.hasError('required') && form.controls.newLeadUserId.touched) {
              <mat-error>Select the new lead.</mat-error>
            }
          </mat-form-field>

          <mat-checkbox formControlName="removeOutgoing">
            Remove the outgoing lead from the team
          </mat-checkbox>
        </form>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="cancel()">Cancel</button>
      <button
        matButton="filled"
        type="button"
        (click)="submit()"
        [disabled]="form.invalid || !data.candidates.length"
      >
        Transfer
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .form {
      display: flex;
      flex-direction: column;
      min-width: 400px;
      gap: 0.5rem;
    }
    .full {
      width: 100%;
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
export class TransferLeadDialogComponent {
  readonly data = inject<TransferLeadDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject<
    MatDialogRef<TransferLeadDialogComponent, TransferLeadDialogResult>
  >(MatDialogRef);
  private readonly fb = inject(FormBuilder);

  readonly form = this.fb.nonNullable.group({
    newLeadUserId: ['', [Validators.required]],
    removeOutgoing: [false],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    this.dialogRef.close({
      newLeadUserId: v.newLeadUserId,
      removeOutgoing: v.removeOutgoing,
    });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
