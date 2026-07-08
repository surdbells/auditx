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
import { MatDatepickerModule } from '@angular/material/datepicker';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';

import { UserLookupService } from '../../../core/services/user-lookup.service';
import { ReferenceDataLookupService } from '../../../core/services/reference-data-lookup.service';
import { AddPlanItemRequest, EntityListItem } from '../../../core/models';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';

export interface PlanItemDialogData {
  /** Universe entities selectable for this plan item. */
  entities: EntityListItem[];
}

function toDateOnly(value: Date | null): string {
  if (!value) {
    return '';
  }
  const y = value.getFullYear();
  const m = String(value.getMonth() + 1).padStart(2, '0');
  const d = String(value.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}

@Component({
  selector: 'app-plan-item-dialog',
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
    TranslatePipe,
  ],
  template: `
    <h2 mat-dialog-title>{{ 'planning.itemDialog.title' | t }}</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="form">
        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ 'planning.itemDialog.entity' | t }}</mat-label>
          <mat-select formControlName="entityId">
            @for (e of data.entities; track e.id) {
              <mat-option [value]="e.id">{{ e.name }}</mat-option>
            }
          </mat-select>
          @if (form.controls.entityId.hasError('required') && form.controls.entityId.touched) {
            <mat-error>{{ 'planning.itemDialog.entityRequired' | t }}</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ 'planning.itemDialog.auditType' | t }}</mat-label>
          <mat-select formControlName="auditType">
            @for (o of auditTypes(); track o.code) {
              <mat-option [value]="o.code">{{ o.label }}</mat-option>
            }
          </mat-select>
          @if (form.controls.auditType.hasError('required') && form.controls.auditType.touched) {
            <mat-error>{{ 'planning.itemDialog.auditTypeRequired' | t }}</mat-error>
          }
        </mat-form-field>

        <div class="row">
          <mat-form-field appearance="outline">
            <mat-label>{{ 'planning.itemDialog.plannedStart' | t }}</mat-label>
            <input matInput [matDatepicker]="startPicker" formControlName="plannedStartDate" />
            <mat-datepicker-toggle matIconSuffix [for]="startPicker" />
            <mat-datepicker #startPicker />
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>{{ 'planning.itemDialog.plannedEnd' | t }}</mat-label>
            <input matInput [matDatepicker]="endPicker" formControlName="plannedEndDate" />
            <mat-datepicker-toggle matIconSuffix [for]="endPicker" />
            <mat-datepicker #endPicker />
          </mat-form-field>
        </div>

        <div class="row">
          <mat-form-field appearance="outline">
            <mat-label>{{ 'planning.itemDialog.estimatedEffort' | t }}</mat-label>
            <input matInput type="number" formControlName="estimatedEffortDays" min="0" />
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>{{ 'planning.itemDialog.assignedLead' | t }}</mat-label>
            <mat-select formControlName="assignedLeadUserId">
              <mat-option [value]="''">{{ 'planning.itemDialog.none' | t }}</mat-option>
              @for (u of userLookup.options(); track u.id) {
                <mat-option [value]="u.id">{{ u.displayName }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
        </div>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="cancel()">{{ 'planning.common.cancel' | t }}</button>
      <button matButton="filled" type="button" (click)="submit()" [disabled]="form.invalid">
        {{ 'planning.common.addItem' | t }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .form {
      display: flex;
      flex-direction: column;
      min-width: 440px;
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
    @media (max-width: 560px) {
      .form {
        min-width: auto;
      }
      .row {
        flex-direction: column;
      }
    }
  `,
})
export class PlanItemDialogComponent {
  readonly data = inject<PlanItemDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef =
    inject<MatDialogRef<PlanItemDialogComponent, AddPlanItemRequest>>(
      MatDialogRef,
    );
  private readonly fb = inject(FormBuilder);
  /** Populates the assigned-lead select (lazy directory load). */
  readonly userLookup = inject(UserLookupService);
  private readonly refLookup = inject(ReferenceDataLookupService);

  /** Active audit-type reference-data items (lazy-loaded). */
  readonly auditTypes = this.refLookup.options('audit_type');

  readonly form = this.fb.nonNullable.group({
    entityId: ['', [Validators.required]],
    auditType: ['', [Validators.required]],
    plannedStartDate: [null as Date | null, [Validators.required]],
    plannedEndDate: [null as Date | null, [Validators.required]],
    estimatedEffortDays: [null as number | null],
    assignedLeadUserId: [''],
  });

  constructor() {
    this.userLookup.ensureLoaded();
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    this.dialogRef.close({
      entityId: v.entityId,
      auditType: v.auditType.trim(),
      plannedStartDate: toDateOnly(v.plannedStartDate),
      plannedEndDate: toDateOnly(v.plannedEndDate),
      estimatedEffortDays:
        v.estimatedEffortDays !== null ? Number(v.estimatedEffortDays) : null,
      assignedLeadUserId: v.assignedLeadUserId.trim() || null,
    });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
