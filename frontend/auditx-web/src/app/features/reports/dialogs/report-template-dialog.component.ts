import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import {
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';

import { CreateReportTemplateRequest } from '../../../core/models';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';

function jsonValidator(control: AbstractControl): ValidationErrors | null {
  const value = (control.value ?? '').trim();
  if (!value) {
    return null;
  }
  try {
    JSON.parse(value);
    return null;
  } catch {
    return { json: true };
  }
}

/** Create a new report template: a name plus a JSON definition editor. */
@Component({
  selector: 'app-report-template-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    TranslatePipe,
  ],
  template: `
    <h2 mat-dialog-title>{{ 'reports.dialog.template.title' | t }}</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="form">
        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ 'reports.dialog.template.nameLabel' | t }}</mat-label>
          <input matInput formControlName="name" autocomplete="off" />
          @if (
            form.controls.name.hasError('required') &&
            form.controls.name.touched
          ) {
            <mat-error>{{ 'reports.dialog.template.nameError' | t }}</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ 'reports.dialog.template.definitionLabel' | t }}</mat-label>
          <textarea
            matInput
            rows="10"
            formControlName="templateDefinitionJson"
            spellcheck="false"
            placeholder='{ "sections": [] }'
          ></textarea>
          @if (
            form.controls.templateDefinitionJson.hasError('required') &&
            form.controls.templateDefinitionJson.touched
          ) {
            <mat-error>{{ 'reports.dialog.template.definitionRequired' | t }}</mat-error>
          } @else if (form.controls.templateDefinitionJson.hasError('json')) {
            <mat-error>{{ 'reports.dialog.template.definitionJsonError' | t }}</mat-error>
          }
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="cancel()">{{ 'reports.common.cancel' | t }}</button>
      <button
        matButton="filled"
        type="button"
        (click)="submit()"
        [disabled]="form.invalid"
      >
        {{ 'reports.common.create' | t }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .form {
      display: flex;
      flex-direction: column;
      min-width: 480px;
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
export class ReportTemplateDialogComponent {
  private readonly dialogRef =
    inject<
      MatDialogRef<ReportTemplateDialogComponent, CreateReportTemplateRequest>
    >(MatDialogRef);
  private readonly fb = inject(FormBuilder);

  readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required]],
    templateDefinitionJson: ['', [Validators.required, jsonValidator]],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    this.dialogRef.close({
      name: v.name.trim(),
      templateDefinitionJson: v.templateDefinitionJson.trim(),
    });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
