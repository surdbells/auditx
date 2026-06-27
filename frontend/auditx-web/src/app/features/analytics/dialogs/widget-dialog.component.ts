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
  MAT_DIALOG_DATA,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';

import { WidgetType } from '../../../core/models';

/** Data passed in: the suggested next position for the new widget. */
export interface WidgetDialogData {
  defaultPosition: number;
}

/** Result emitted: the widget shape (the caller adds the dashboard `version`). */
export interface WidgetDialogResult {
  widgetType: WidgetType;
  metricKey: string;
  title: string;
  targetRoleId?: string | null;
  position: number;
  configJson?: string | null;
}

const WIDGET_TYPES: { value: WidgetType; label: string }[] = [
  { value: 'single_metric', label: 'Single metric' },
  { value: 'table', label: 'Table' },
  { value: 'chart', label: 'Chart' },
];

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

/** Configure a dashboard widget: type, metric key, title, position, config. */
@Component({
  selector: 'app-widget-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
  ],
  template: `
    <h2 mat-dialog-title>Add widget</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="form">
        <mat-form-field appearance="outline" class="full">
          <mat-label>Title</mat-label>
          <input matInput formControlName="title" autocomplete="off" />
          @if (form.controls.title.hasError('required')) {
            <mat-error>A title is required.</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline" class="full">
          <mat-label>Widget type</mat-label>
          <mat-select formControlName="widgetType">
            @for (t of widgetTypes; track t.value) {
              <mat-option [value]="t.value">{{ t.label }}</mat-option>
            }
          </mat-select>
        </mat-form-field>

        <mat-form-field appearance="outline" class="full">
          <mat-label>Metric key</mat-label>
          <input
            matInput
            formControlName="metricKey"
            autocomplete="off"
            placeholder="e.g. function_performance"
          />
          <mat-hint>
            Known keys: function_performance, exception_portfolio,
            sanctions_consistency, material_findings, plan_status, coverage,
            performance_scorecards, recurrence_clusters
          </mat-hint>
          @if (form.controls.metricKey.hasError('required')) {
            <mat-error>A metric key is required.</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline" class="full">
          <mat-label>Position</mat-label>
          <input
            matInput
            type="number"
            min="0"
            formControlName="position"
          />
        </mat-form-field>

        <mat-form-field appearance="outline" class="full">
          <mat-label>Target role ID (optional)</mat-label>
          <input matInput formControlName="targetRoleId" autocomplete="off" />
        </mat-form-field>

        <mat-form-field appearance="outline" class="full">
          <mat-label>Config (JSON, optional)</mat-label>
          <textarea
            matInput
            rows="4"
            formControlName="configJson"
            spellcheck="false"
            placeholder='{ "windowMonths": 12 }'
          ></textarea>
          @if (form.controls.configJson.hasError('json')) {
            <mat-error>Must be valid JSON.</mat-error>
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
        [disabled]="form.invalid"
      >
        Add
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
export class WidgetDialogComponent {
  private readonly data = inject<WidgetDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef =
    inject<MatDialogRef<WidgetDialogComponent, WidgetDialogResult>>(
      MatDialogRef,
    );
  private readonly fb = inject(FormBuilder);

  readonly widgetTypes = WIDGET_TYPES;

  readonly form = this.fb.nonNullable.group({
    title: ['', [Validators.required]],
    widgetType: ['single_metric' as WidgetType, [Validators.required]],
    metricKey: ['', [Validators.required]],
    position: [this.data?.defaultPosition ?? 0, [Validators.required]],
    targetRoleId: [''],
    configJson: ['', [jsonValidator]],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    this.dialogRef.close({
      title: v.title.trim(),
      widgetType: v.widgetType,
      metricKey: v.metricKey.trim(),
      position: Number(v.position),
      targetRoleId: v.targetRoleId.trim() || null,
      configJson: v.configJson.trim() || null,
    });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
