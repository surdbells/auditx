import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import {
  MAT_DIALOG_DATA,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';

import { ReferenceDataLookupService } from '../../../../core/services/reference-data-lookup.service';
import {
  CreateRootCauseGapRequest,
  RootCauseGap,
  UpdateRootCauseGapRequest,
  UserDto,
} from '../../../../core/models';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';

export interface RootCauseGapDialogData {
  /** Present when editing; absent for create. */
  gap?: RootCauseGap;
  /** Selectable owners. */
  users: UserDto[];
}

export type RootCauseGapDialogResult =
  | { mode: 'create'; body: CreateRootCauseGapRequest }
  | { mode: 'edit'; id: string; body: UpdateRootCauseGapRequest };

function toDateOnly(value: Date | null): string | null {
  if (!value) {
    return null;
  }
  const y = value.getFullYear();
  const m = String(value.getMonth() + 1).padStart(2, '0');
  const d = String(value.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}

@Component({
  selector: 'app-root-cause-gap-dialog',
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
    <h2 mat-dialog-title>
      {{ (isEdit ? 'rootCauseGaps.dialog.editTitle' : 'rootCauseGaps.dialog.newTitle') | t }}
    </h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="form">
        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ 'rootCauseGaps.field.title' | t }}</mat-label>
          <input matInput formControlName="title" autocomplete="off" />
          @if (form.controls.title.hasError('required') && form.controls.title.touched) {
            <mat-error>{{ 'rootCauseGaps.error.titleRequired' | t }}</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ 'rootCauseGaps.field.description' | t }}</mat-label>
          <textarea matInput formControlName="description" rows="3"></textarea>
        </mat-form-field>

        <div class="row">
          <mat-form-field appearance="outline">
            <mat-label>{{ 'rootCauseGaps.field.category' | t }}</mat-label>
            <mat-select formControlName="category">
              <mat-option [value]="''">{{ 'rootCauseGaps.field.noCategory' | t }}</mat-option>
              @for (o of rootCauses(); track o.code) {
                <mat-option [value]="o.code">{{ o.label }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>{{ 'rootCauseGaps.field.owner' | t }}</mat-label>
            <mat-select formControlName="ownerUserId">
              @for (u of data.users; track u.id) {
                <mat-option [value]="u.id">{{ u.displayName }}</mat-option>
              }
            </mat-select>
            @if (form.controls.ownerUserId.hasError('required') && form.controls.ownerUserId.touched) {
              <mat-error>{{ 'rootCauseGaps.error.ownerRequired' | t }}</mat-error>
            }
          </mat-form-field>
        </div>

        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ 'rootCauseGaps.field.targetDate' | t }}</mat-label>
          <input matInput [matDatepicker]="picker" formControlName="targetDate" />
          <mat-datepicker-toggle matIconSuffix [for]="picker" />
          <mat-datepicker #picker />
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="cancel()">{{ 'rootCauseGaps.actions.cancel' | t }}</button>
      <button matButton="filled" type="button" (click)="submit()" [disabled]="form.invalid">
        {{ (isEdit ? 'rootCauseGaps.actions.save' : 'rootCauseGaps.actions.create') | t }}
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
export class RootCauseGapDialogComponent {
  readonly data = inject<RootCauseGapDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef =
    inject<MatDialogRef<RootCauseGapDialogComponent, RootCauseGapDialogResult>>(MatDialogRef);
  private readonly fb = inject(FormBuilder);
  private readonly refLookup = inject(ReferenceDataLookupService);

  readonly isEdit = !!this.data.gap;
  readonly rootCauses = this.refLookup.options('root_cause_category');

  readonly form = this.fb.nonNullable.group({
    title: [this.data.gap?.title ?? '', [Validators.required, Validators.maxLength(255)]],
    description: [this.data.gap?.description ?? ''],
    category: [this.data.gap?.category ?? ''],
    ownerUserId: [this.data.gap?.ownerUserId ?? '', [Validators.required]],
    targetDate: [this.data.gap?.targetDate ? new Date(this.data.gap.targetDate) : (null as Date | null)],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    const common = {
      title: v.title.trim(),
      description: v.description.trim() || null,
      category: v.category.trim() || null,
      ownerUserId: v.ownerUserId,
      targetDate: toDateOnly(v.targetDate),
    };

    if (this.isEdit && this.data.gap) {
      this.dialogRef.close({ mode: 'edit', id: this.data.gap.id, body: { ...common, version: this.data.gap.version } });
    } else {
      this.dialogRef.close({ mode: 'create', body: common });
    }
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
