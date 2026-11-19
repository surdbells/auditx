import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { fromDateOnly, toDateOnly } from '../../exceptions/dialogs/date-reason-dialog.component';

export interface EditScopeDialogData {
  name: string;
  scopeDescription: string | null;
  objectives: string | null;
  startDate: string;
  targetEndDate: string | null;
  /** The programme is approved: scope and objectives are what was approved and cannot change here. */
  scopeLocked: boolean;
}

export interface EditScopeDialogResult {
  name: string;
  scopeDescription: string | null;
  objectives: string | null;
  startDate: string;
  targetEndDate: string;
}

/** Edits the engagement's name, scope, objectives and dates while it is a draft or planned. */
@Component({
  selector: 'app-edit-scope-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [provideNativeDateAdapter()],
  imports: [ReactiveFormsModule, MatDialogModule, MatFormFieldModule, MatInputModule, MatDatepickerModule, MatButtonModule, TranslatePipe],
  template: `
    <h2 mat-dialog-title>{{ 'audits.scope.editTitle' | t }}</h2>
    <mat-dialog-content>
      @if (data.scopeLocked) {
        <p class="note">{{ 'audits.scope.lockedNote' | t }}</p>
      }
      <form [formGroup]="form" class="form">
        <mat-form-field appearance="outline">
          <mat-label>{{ 'audits.field.name' | t }}</mat-label>
          <input matInput formControlName="name" maxlength="255" required />
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>{{ 'audits.field.scope' | t }}</mat-label>
          <textarea matInput formControlName="scopeDescription" rows="4" maxlength="4000"></textarea>
          <mat-hint>{{ 'audits.scope.scopeHint' | t }}</mat-hint>
        </mat-form-field>
        <mat-form-field appearance="outline">
          <mat-label>{{ 'audits.field.objectives' | t }}</mat-label>
          <textarea matInput formControlName="objectives" rows="4" maxlength="4000"></textarea>
          <mat-hint>{{ 'audits.scope.objectivesHint' | t }}</mat-hint>
        </mat-form-field>
        <div class="dates">
          <mat-form-field appearance="outline">
            <mat-label>{{ 'audits.field.startDate' | t }}</mat-label>
            <input matInput [matDatepicker]="startPicker" formControlName="startDate" required />
            <mat-datepicker-toggle matIconSuffix [for]="startPicker" />
            <mat-datepicker #startPicker />
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>{{ 'audits.field.targetEndDate' | t }}</mat-label>
            <input matInput [matDatepicker]="endPicker" formControlName="targetEndDate" required />
            <mat-datepicker-toggle matIconSuffix [for]="endPicker" />
            <mat-datepicker #endPicker />
          </mat-form-field>
        </div>
        @if (datesInverted()) {
          <p class="error">{{ 'audits.scope.datesError' | t }}</p>
        }
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="cancel()">{{ 'common.cancel' | t }}</button>
      <button matButton="filled" type="button" [disabled]="form.invalid || datesInverted()" (click)="submit()">
        {{ 'common.save' | t }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .form { display: flex; flex-direction: column; gap: 4px; }
    .dates { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }
    .note { margin: 0 0 12px; color: var(--mat-sys-on-surface-variant); }
    .error { margin: 0; color: var(--mat-sys-error); font-size: 0.875rem; }
    @media (max-width: 520px) { .dates { grid-template-columns: 1fr; } }
  `,
})
export class EditScopeDialogComponent {
  readonly data = inject<EditScopeDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject<MatDialogRef<EditScopeDialogComponent, EditScopeDialogResult>>(MatDialogRef);
  private readonly fb = inject(FormBuilder);

  readonly form = this.fb.group({
    name: [this.data.name, [Validators.required, Validators.maxLength(255)]],
    scopeDescription: [{ value: this.data.scopeDescription ?? '', disabled: this.data.scopeLocked }, [Validators.maxLength(4000)]],
    objectives: [{ value: this.data.objectives ?? '', disabled: this.data.scopeLocked }, [Validators.maxLength(4000)]],
    startDate: [fromDateOnly(this.data.startDate) as Date | null, [Validators.required]],
    targetEndDate: [this.data.targetEndDate ? fromDateOnly(this.data.targetEndDate) : (null as Date | null), [Validators.required]],
  });

  datesInverted(): boolean {
    const { startDate, targetEndDate } = this.form.getRawValue();
    return !!startDate && !!targetEndDate && targetEndDate < startDate;
  }

  submit(): void {
    const v = this.form.getRawValue();
    if (this.form.invalid || this.datesInverted() || !v.startDate || !v.targetEndDate) {
      this.form.markAllAsTouched();
      return;
    }
    this.dialogRef.close({
      name: (v.name ?? '').trim(),
      scopeDescription: (v.scopeDescription ?? '').trim() || null,
      objectives: (v.objectives ?? '').trim() || null,
      startDate: toDateOnly(v.startDate),
      targetEndDate: toDateOnly(v.targetEndDate),
    });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
