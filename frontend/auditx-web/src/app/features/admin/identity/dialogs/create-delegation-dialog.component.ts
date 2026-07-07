import {
  ChangeDetectionStrategy,
  Component,
  inject,
} from '@angular/core';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import {
  MAT_DIALOG_DATA,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';

import { UserLookupService } from '../../../../core/services/user-lookup.service';
import { CreateDelegationRequest, RoleDto } from '../../../../core/models';

export interface CreateDelegationDialogData {
  fromUserDisplayName: string;
  roles: RoleDto[];
}

function dateRangeValidator(group: AbstractControl): ValidationErrors | null {
  const start = group.get('startDate')?.value as Date | null;
  const end = group.get('endDate')?.value as Date | null;
  if (start && end && end.getTime() < start.getTime()) {
    return { dateRange: true };
  }
  return null;
}

@Component({
  selector: 'app-create-delegation-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [provideNativeDateAdapter()],
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatDatepickerModule,
    MatButtonModule,
  ],
  template: `
    <h2 mat-dialog-title>Delegate a role</h2>
    <mat-dialog-content>
      <p class="hint">
        Temporarily delegate one of
        <strong>{{ data.fromUserDisplayName }}</strong>'s roles to another user for a
        fixed period.
      </p>
      <form [formGroup]="form" class="form">
        <mat-form-field appearance="outline" class="full">
          <mat-label>Delegate to</mat-label>
          <mat-select formControlName="toUserId">
            @for (u of userLookup.options(); track u.id) {
              <mat-option [value]="u.id">{{ u.displayName }}</mat-option>
            }
          </mat-select>
          @if (form.controls.toUserId.hasError('required') && form.controls.toUserId.touched) {
            <mat-error>A target user is required.</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline" class="full">
          <mat-label>Role</mat-label>
          <mat-select formControlName="roleId">
            @for (role of data.roles; track role.id) {
              <mat-option [value]="role.id">{{ role.name }}</mat-option>
            }
          </mat-select>
          @if (form.controls.roleId.hasError('required') && form.controls.roleId.touched) {
            <mat-error>Please select a role.</mat-error>
          }
        </mat-form-field>

        <div class="row">
          <mat-form-field appearance="outline">
            <mat-label>Start date</mat-label>
            <input matInput [matDatepicker]="startPicker" formControlName="startDate" />
            <mat-datepicker-toggle matIconSuffix [for]="startPicker" />
            <mat-datepicker #startPicker />
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>End date</mat-label>
            <input matInput [matDatepicker]="endPicker" formControlName="endDate" />
            <mat-datepicker-toggle matIconSuffix [for]="endPicker" />
            <mat-datepicker #endPicker />
          </mat-form-field>
        </div>

        @if (form.hasError('dateRange')) {
          <p class="error-text">End date must be on or after the start date.</p>
        }
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="cancel()">Cancel</button>
      <button matButton="filled" type="button" (click)="submit()" [disabled]="form.invalid">
        Create delegation
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .form {
      display: flex;
      flex-direction: column;
      min-width: 380px;
    }
    .full {
      width: 100%;
    }
    .row {
      display: flex;
      gap: 1rem;
      flex-wrap: wrap;
    }
    .row mat-form-field {
      flex: 1 1 160px;
    }
    .hint {
      color: var(--mat-sys-on-surface-variant);
      margin-top: 0;
    }
    .error-text {
      color: var(--mat-sys-error);
      font: var(--mat-sys-body-small);
      margin: 0;
    }
    @media (max-width: 480px) {
      .form {
        min-width: auto;
      }
    }
  `,
})
export class CreateDelegationDialogComponent {
  readonly data = inject<CreateDelegationDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef =
    inject<MatDialogRef<CreateDelegationDialogComponent, CreateDelegationRequest>>(
      MatDialogRef,
    );
  private readonly fb = inject(FormBuilder);
  /** Populates the delegate-to select (lazy directory load). */
  readonly userLookup = inject(UserLookupService);

  readonly form = this.fb.nonNullable.group(
    {
      toUserId: ['', [Validators.required]],
      roleId: ['', [Validators.required]],
      startDate: [null as Date | null, [Validators.required]],
      endDate: [null as Date | null, [Validators.required]],
    },
    { validators: dateRangeValidator },
  );

  constructor() {
    this.userLookup.ensureLoaded();
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const { toUserId, roleId, startDate, endDate } = this.form.getRawValue();
    const result: CreateDelegationRequest = {
      toUserId: toUserId.trim(),
      roleId,
      startDate: (startDate as Date).toISOString(),
      endDate: (endDate as Date).toISOString(),
    };
    this.dialogRef.close(result);
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
