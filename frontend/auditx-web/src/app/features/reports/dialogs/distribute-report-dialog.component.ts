import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule, MatChipInputEvent } from '@angular/material/chips';
import {
  MAT_DIALOG_DATA,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';

import { DistributeReportRequest, UserDto } from '../../../core/models';

export interface DistributeReportDialogData {
  /** Active users for the recipient picker. */
  users: UserDto[];
}

const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

/**
 * Distribute a report version to internal users (picker) and/or free-text
 * email addresses (chips). No distribution lists, no SMS.
 */
@Component({
  selector: 'app-distribute-report-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatChipsModule,
    MatButtonModule,
    MatIconModule,
  ],
  template: `
    <h2 mat-dialog-title>Distribute report</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="form">
        <mat-form-field appearance="outline" class="full">
          <mat-label>Recipients (users)</mat-label>
          <mat-select formControlName="userIds" multiple>
            @for (u of data.users; track u.id) {
              <mat-option [value]="u.id">
                {{ u.displayName }} ({{ u.email }})
              </mat-option>
            }
          </mat-select>
          <mat-hint>Select one or more internal recipients.</mat-hint>
        </mat-form-field>

        <mat-form-field appearance="outline" class="full">
          <mat-label>External email addresses</mat-label>
          <mat-chip-grid #chipGrid aria-label="Email addresses">
            @for (email of emails(); track email) {
              <mat-chip-row (removed)="removeEmail(email)">
                {{ email }}
                <button matChipRemove [attr.aria-label]="'Remove ' + email">
                  <mat-icon>cancel</mat-icon>
                </button>
              </mat-chip-row>
            }
            <input
              placeholder="e.g. board@bank.test"
              [matChipInputFor]="chipGrid"
              (matChipInputTokenEnd)="addEmail($event)"
            />
          </mat-chip-grid>
          @if (emailError()) {
            <mat-error>Enter a valid email address.</mat-error>
          } @else {
            <mat-hint>Press Enter to add an address.</mat-hint>
          }
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="cancel()">Cancel</button>
      <button
        matButton="filled"
        type="button"
        (click)="submit()"
        [disabled]="!canSubmit()"
      >
        Distribute
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .form {
      display: flex;
      flex-direction: column;
      min-width: 460px;
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
export class DistributeReportDialogComponent {
  readonly data = inject<DistributeReportDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef =
    inject<
      MatDialogRef<DistributeReportDialogComponent, DistributeReportRequest>
    >(MatDialogRef);
  private readonly fb = inject(FormBuilder);

  readonly emails = signal<string[]>([]);
  readonly emailError = signal(false);

  readonly form = this.fb.nonNullable.group({
    userIds: [[] as string[]],
  });

  canSubmit(): boolean {
    return this.form.getRawValue().userIds.length > 0 || this.emails().length > 0;
  }

  addEmail(event: MatChipInputEvent): void {
    const value = (event.value ?? '').trim();
    if (!value) {
      event.chipInput?.clear();
      return;
    }
    if (!EMAIL_RE.test(value)) {
      this.emailError.set(true);
      return;
    }
    this.emailError.set(false);
    if (!this.emails().includes(value)) {
      this.emails.update((list) => [...list, value]);
    }
    event.chipInput?.clear();
  }

  removeEmail(email: string): void {
    this.emails.update((list) => list.filter((e) => e !== email));
  }

  submit(): void {
    if (!this.canSubmit()) {
      return;
    }
    this.dialogRef.close({
      recipientUserIds: this.form.getRawValue().userIds,
      recipientEmailAddresses: this.emails(),
    });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
