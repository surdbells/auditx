import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
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
import { MatRadioModule } from '@angular/material/radio';
import { MatSelectModule } from '@angular/material/select';

import {
  PlanItem,
  PlanRevisionKind,
  SubmitRevisionRequest,
} from '../../../core/models';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';

export interface SubmitRevisionDialogData {
  items: PlanItem[];
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

/**
 * Submit a revision against an approved/in-flight plan. A `minor` revision
 * shifts a single item's dates; a `material` revision re-opens the plan for
 * structural change and re-approval.
 */
@Component({
  selector: 'app-submit-revision-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [provideNativeDateAdapter()],
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatRadioModule,
    MatDatepickerModule,
    MatButtonModule,
    TranslatePipe,
  ],
  template: `
    <h2 mat-dialog-title>{{ 'planning.revisionDialog.title' | t }}</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="form">
        <mat-radio-group formControlName="kind" class="kind">
          <mat-radio-button value="minor">
            {{ 'planning.revisionDialog.minor' | t }}
          </mat-radio-button>
          <mat-radio-button value="material">
            {{ 'planning.revisionDialog.material' | t }}
          </mat-radio-button>
        </mat-radio-group>

        @if (isMinor()) {
          <mat-form-field appearance="outline" class="full">
            <mat-label>{{ 'planning.revisionDialog.item' | t }}</mat-label>
            <mat-select formControlName="itemId">
              @for (i of data.items; track i.id) {
                <mat-option [value]="i.id">
                  {{ i.auditType }} — {{ i.plannedStartDate }}
                </mat-option>
              }
            </mat-select>
            @if (form.controls.itemId.hasError('required') && form.controls.itemId.touched) {
              <mat-error>{{ 'planning.revisionDialog.itemRequired' | t }}</mat-error>
            }
          </mat-form-field>

          <div class="row">
            <mat-form-field appearance="outline">
              <mat-label>{{ 'planning.revisionDialog.newStart' | t }}</mat-label>
              <input matInput [matDatepicker]="startPicker" formControlName="newStartDate" />
              <mat-datepicker-toggle matIconSuffix [for]="startPicker" />
              <mat-datepicker #startPicker />
            </mat-form-field>
            <mat-form-field appearance="outline">
              <mat-label>{{ 'planning.revisionDialog.newEnd' | t }}</mat-label>
              <input matInput [matDatepicker]="endPicker" formControlName="newEndDate" />
              <mat-datepicker-toggle matIconSuffix [for]="endPicker" />
              <mat-datepicker #endPicker />
            </mat-form-field>
          </div>
        } @else {
          <p class="hint">
            {{ 'planning.revisionDialog.materialHint' | t }}
          </p>
        }
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="cancel()">{{ 'planning.common.cancel' | t }}</button>
      <button matButton="filled" type="button" (click)="submit()" [disabled]="invalid()">
        {{ 'planning.common.submit' | t }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .form {
      display: flex;
      flex-direction: column;
      min-width: 440px;
      gap: 0.5rem;
    }
    .kind {
      display: flex;
      flex-direction: column;
      margin-bottom: 0.5rem;
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
    .hint {
      color: var(--mat-sys-on-surface-variant);
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
export class SubmitRevisionDialogComponent {
  readonly data = inject<SubmitRevisionDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef =
    inject<MatDialogRef<SubmitRevisionDialogComponent, SubmitRevisionRequest>>(
      MatDialogRef,
    );
  private readonly fb = inject(FormBuilder);

  readonly form = this.fb.nonNullable.group({
    kind: ['minor' as PlanRevisionKind, [Validators.required]],
    itemId: [''],
    newStartDate: [null as Date | null],
    newEndDate: [null as Date | null],
  });

  readonly kindSignal = signal<PlanRevisionKind>('minor');

  readonly isMinor = computed(() => this.kindSignal() === 'minor');

  readonly invalid = computed(() => {
    if (!this.isMinor()) {
      return false;
    }
    const v = this.form.getRawValue();
    return !v.itemId || !v.newStartDate || !v.newEndDate;
  });

  constructor() {
    this.form.controls.kind.valueChanges.subscribe((k) =>
      this.kindSignal.set(k),
    );
  }

  submit(): void {
    const v = this.form.getRawValue();
    if (v.kind === 'minor') {
      if (!v.itemId || !v.newStartDate || !v.newEndDate) {
        this.form.markAllAsTouched();
        return;
      }
      this.dialogRef.close({
        kind: 'minor',
        itemId: v.itemId,
        newStartDate: toDateOnly(v.newStartDate),
        newEndDate: toDateOnly(v.newEndDate),
      });
    } else {
      this.dialogRef.close({ kind: 'material' });
    }
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
