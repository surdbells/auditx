import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
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

import { IconComponent } from '../../../core/icons/icon.component';
import { ReferenceDataLookupService } from '../../../core/services/reference-data-lookup.service';
import { CreateSelfAssessmentRequest, TemplateListItem } from '../../../core/models';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';

export interface SelfAssessmentDialogData {
  /** Published templates — one MUST be chosen; it seeds the self-assessment's checklist. */
  templates: TemplateListItem[];
}

/** Converts a Date to an ISO `yyyy-MM-dd` DateOnly string. */
function toDateOnly(value: Date | null): string {
  if (!value) {
    return '';
  }
  const y = value.getFullYear();
  const m = String(value.getMonth() + 1).padStart(2, '0');
  const d = String(value.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}

/**
 * Dialog for starting a self-assessment. Unlike a normal audit, there is no lead / auditee / team choice — the
 * current user is both the assessor and the subject. A published template is required (the assessor cannot
 * hand-author a checklist), so the template field is mandatory and drives the audit type.
 */
@Component({
  selector: 'app-self-assessment-dialog',
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
    IconComponent,
    TranslatePipe,
  ],
  template: `
    <h2 mat-dialog-title>{{ 'audits.selfAssessment.dialogTitle' | t }}</h2>
    <mat-dialog-content>
      <div class="sa-banner">
        <app-icon name="fact_check" />
        <span>{{ 'audits.selfAssessment.dialogHint' | t }}</span>
      </div>
      <form [formGroup]="form" class="form">
        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ 'audits.selfAssessment.field.template' | t }}</mat-label>
          <mat-select formControlName="templateId" (selectionChange)="onTemplateSelected($event.value)">
            @for (t of data.templates; track t.id) {
              <mat-option [value]="t.id">{{ t.name }} ({{ t.auditType }})</mat-option>
            }
          </mat-select>
          @if (form.controls.templateId.hasError('required') && form.controls.templateId.touched) {
            <mat-error>{{ 'audits.selfAssessment.error.template' | t }}</mat-error>
          }
          <mat-hint>{{ 'audits.selfAssessment.hint.template' | t }}</mat-hint>
        </mat-form-field>

        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ 'audits.field.name' | t }}</mat-label>
          <input matInput formControlName="name" autocomplete="off" />
          @if (form.controls.name.hasError('required') && form.controls.name.touched) {
            <mat-error>{{ 'audits.selfAssessment.error.name' | t }}</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ 'audits.field.auditType' | t }}</mat-label>
          <mat-select formControlName="auditType">
            @for (o of auditTypes(); track o.code) {
              <mat-option [value]="o.code">{{ o.label }}</mat-option>
            }
          </mat-select>
        </mat-form-field>

        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ 'audits.field.scope' | t }}</mat-label>
          <textarea matInput formControlName="scopeDescription" rows="2"></textarea>
        </mat-form-field>

        <div class="row">
          <mat-form-field appearance="outline">
            <mat-label>{{ 'audits.field.startDate' | t }}</mat-label>
            <input matInput [matDatepicker]="startPicker" formControlName="startDate" />
            <mat-datepicker-toggle matIconSuffix [for]="startPicker" />
            <mat-datepicker #startPicker />
            @if (form.controls.startDate.hasError('required') && form.controls.startDate.touched) {
              <mat-error>{{ 'audits.selfAssessment.error.startDate' | t }}</mat-error>
            }
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>{{ 'audits.field.targetEndDate' | t }}</mat-label>
            <input matInput [matDatepicker]="endPicker" formControlName="targetEndDate" />
            <mat-datepicker-toggle matIconSuffix [for]="endPicker" />
            <mat-datepicker #endPicker />
          </mat-form-field>
        </div>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="cancel()">{{ 'common.cancel' | t }}</button>
      <button matButton="filled" type="button" (click)="submit()" [disabled]="form.invalid">
        {{ 'audits.selfAssessment.action.start' | t }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .sa-banner {
      display: flex;
      align-items: flex-start;
      gap: 0.6rem;
      padding: 0.75rem 1rem;
      margin-bottom: 1rem;
      border-radius: var(--ax-radius, 12px);
      background: var(--mat-sys-secondary-container);
      color: var(--mat-sys-on-secondary-container);
      font: var(--mat-sys-body-small);
    }
    .sa-banner app-icon {
      flex: 0 0 auto;
    }
    .form {
      display: flex;
      flex-direction: column;
      min-width: 460px;
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
export class SelfAssessmentDialogComponent {
  readonly data = inject<SelfAssessmentDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef =
    inject<MatDialogRef<SelfAssessmentDialogComponent, CreateSelfAssessmentRequest>>(MatDialogRef);
  private readonly fb = inject(FormBuilder);
  private readonly refLookup = inject(ReferenceDataLookupService);

  /** Active audit-type reference-data items (lazy-loaded). */
  readonly auditTypes = this.refLookup.options('audit_type');

  readonly form = this.fb.nonNullable.group({
    templateId: ['', [Validators.required]],
    name: ['', [Validators.required, Validators.maxLength(200)]],
    auditType: ['', [Validators.required]],
    scopeDescription: [''],
    startDate: [new Date() as Date | null, [Validators.required]],
    targetEndDate: [null as Date | null],
  });

  /** Selecting a template aligns the audit type to that template's type. */
  onTemplateSelected(templateId: string): void {
    const template = this.data.templates.find((t) => t.id === templateId);
    if (template) {
      this.form.controls.auditType.setValue(template.auditType);
    }
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    this.dialogRef.close({
      templateId: v.templateId,
      name: v.name.trim(),
      auditType: v.auditType.trim(),
      scopeDescription: v.scopeDescription.trim() || null,
      startDate: toDateOnly(v.startDate),
      targetEndDate: v.targetEndDate ? toDateOnly(v.targetEndDate) : null,
    });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
