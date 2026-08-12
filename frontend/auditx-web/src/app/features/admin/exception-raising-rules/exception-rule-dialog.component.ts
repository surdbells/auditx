import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import {
  MAT_DIALOG_DATA,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { SelectAllDirective } from '../../../shared/directives/select-all.directive';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';

import {
  CreateExceptionRaisingRuleRequest,
  ExceptionRaisingRule,
  ResponseType,
  UpdateExceptionRaisingRuleRequest,
} from '../../../core/models';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';

export interface ExceptionRuleDialogData {
  /** Present when editing; absent for create. Response types already covered by a rule (to exclude from the create picker). */
  rule?: ExceptionRaisingRule;
  existingResponseTypes: ResponseType[];
}

export type ExceptionRuleDialogResult =
  | { mode: 'create'; body: CreateExceptionRaisingRuleRequest }
  | { mode: 'edit'; id: string; body: UpdateExceptionRaisingRuleRequest };

/** Every response type; value types are the only ones that can carry a score, so score-gating only makes sense there. */
const RESPONSE_TYPES: ResponseType[] = [
  'pass_fail_na',
  'yes_no',
  'text',
  'numeric',
  'date',
  'rating',
  'multiple_choice',
];

@Component({
  selector: 'app-exception-rule-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    SelectAllDirective,
    MatSlideToggleModule,
    MatButtonModule,
    TranslatePipe,
  ],
  template: `
    <h2 mat-dialog-title>
      {{ (isEdit ? 'exceptionRules.dialog.editTitle' : 'exceptionRules.dialog.newTitle') | t }}
    </h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="form">
        @if (!isEdit) {
          <mat-form-field appearance="outline" class="full">
            <mat-label>{{ 'exceptionRules.field.responseType' | t }}</mat-label>
            <mat-select formControlName="responseType">
              @for (rt of availableTypes; track rt) {
                <mat-option [value]="rt">{{ responseTypeLabel(rt) }}</mat-option>
              }
            </mat-select>
            @if (form.controls.responseType.hasError('required') && form.controls.responseType.touched) {
              <mat-error>{{ 'exceptionRules.error.responseTypeRequired' | t }}</mat-error>
            }
          </mat-form-field>
        } @else {
          <p class="type-label">{{ responseTypeLabel(data.rule!.responseType) }}</p>
        }

        <mat-slide-toggle formControlName="allowOnNa">{{ 'exceptionRules.field.allowOnNa' | t }}</mat-slide-toggle>

        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ 'exceptionRules.field.scoreThreshold' | t }}</mat-label>
          <input matInput type="number" min="0" max="100" formControlName="scoreThreshold" />
          <mat-hint>{{ 'exceptionRules.field.scoreThresholdHint' | t }}</mat-hint>
          @if (form.controls.scoreThreshold.hasError('min') || form.controls.scoreThreshold.hasError('max')) {
            <mat-error>{{ 'exceptionRules.error.scoreRange' | t }}</mat-error>
          }
        </mat-form-field>

        @if (isEdit) {
          <mat-slide-toggle formControlName="isActive">{{ 'universe.filter.active' | t }}</mat-slide-toggle>
        }
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="cancel()">{{ 'universe.actions.cancel' | t }}</button>
      <button matButton="filled" type="button" (click)="submit()" [disabled]="form.invalid">
        {{ (isEdit ? 'universe.actions.save' : 'universe.actions.create') | t }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .form {
      display: flex;
      flex-direction: column;
      min-width: 420px;
      gap: 0.75rem;
    }
    .full {
      width: 100%;
    }
    .type-label {
      margin: 0;
      font-weight: 500;
    }
    @media (max-width: 480px) {
      .form {
        min-width: auto;
      }
    }
  `,
})
export class ExceptionRuleDialogComponent {
  readonly data = inject<ExceptionRuleDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef =
    inject<MatDialogRef<ExceptionRuleDialogComponent, ExceptionRuleDialogResult>>(MatDialogRef);
  private readonly fb = inject(FormBuilder);
  private readonly i18n = inject(TranslationService);

  readonly isEdit = !!this.data.rule;

  /** On create, hide response types that already have a rule (the backend enforces one rule per type). */
  readonly availableTypes = RESPONSE_TYPES.filter(
    (rt) => !this.data.existingResponseTypes.includes(rt),
  );

  readonly form = this.fb.nonNullable.group({
    responseType: [
      (this.data.rule?.responseType ?? this.availableTypes[0] ?? 'pass_fail_na') as ResponseType,
      [Validators.required],
    ],
    allowOnNa: [this.data.rule?.allowOnNa ?? false],
    scoreThreshold: [
      this.data.rule?.scoreThreshold ?? (null as number | null),
      [Validators.min(0), Validators.max(100)],
    ],
    isActive: [this.data.rule?.isActive ?? true],
  });

  responseTypeLabel(rt: ResponseType): string {
    const key = `audits.responseType.${rt}`;
    const label = this.i18n.translate(key);
    return label === key ? rt.replace(/_/g, ' ') : label;
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    const threshold =
      v.scoreThreshold === null || v.scoreThreshold === undefined || (v.scoreThreshold as unknown) === ''
        ? null
        : Number(v.scoreThreshold);

    if (this.isEdit && this.data.rule) {
      this.dialogRef.close({
        mode: 'edit',
        id: this.data.rule.id,
        body: { allowOnNa: v.allowOnNa, scoreThreshold: threshold, isActive: v.isActive },
      });
    } else {
      this.dialogRef.close({
        mode: 'create',
        body: { responseType: v.responseType, allowOnNa: v.allowOnNa, scoreThreshold: threshold },
      });
    }
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
