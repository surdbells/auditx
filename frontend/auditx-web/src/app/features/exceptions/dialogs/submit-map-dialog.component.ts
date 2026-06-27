import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import {
  FormArray,
  FormBuilder,
  FormGroup,
  ReactiveFormsModule,
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
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';

import { MapActionInput, UserDto } from '../../../core/models';

export interface SubmitMapDialogData {
  /** Users selectable as per-action owners. */
  users: UserDto[];
}

export interface SubmitMapDialogResult {
  actions: MapActionInput[];
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

type ActionGroup = FormGroup<{
  description: ReturnType<FormBuilder['nonNullable']['control']>;
  ownerUserId: ReturnType<FormBuilder['nonNullable']['control']>;
  targetDate: ReturnType<FormBuilder['nonNullable']['control']>;
  expectedEvidenceType: ReturnType<FormBuilder['nonNullable']['control']>;
}>;

@Component({
  selector: 'app-submit-map-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [provideNativeDateAdapter()],
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatDatepickerModule,
    MatIconModule,
    MatButtonModule,
  ],
  template: `
    <h2 mat-dialog-title>Submit management action plan</h2>
    <mat-dialog-content>
      <form [formGroup]="form">
        <div formArrayName="actions" class="actions">
          @for (group of actions.controls; track group; let i = $index) {
            <div [formGroupName]="i" class="action">
              <div class="action__head">
                <span class="action__title">Action {{ i + 1 }}</span>
                @if (actions.length > 1) {
                  <button
                    matIconButton
                    type="button"
                    (click)="removeAction(i)"
                    aria-label="Remove action"
                  >
                    <mat-icon>delete</mat-icon>
                  </button>
                }
              </div>

              <mat-form-field appearance="outline" class="full">
                <mat-label>Description</mat-label>
                <textarea matInput formControlName="description" rows="2"></textarea>
                @if (group.controls.description.hasError('required') && group.controls.description.touched) {
                  <mat-error>A description is required.</mat-error>
                }
              </mat-form-field>

              <div class="row">
                <mat-form-field appearance="outline">
                  <mat-label>Owner</mat-label>
                  <mat-select formControlName="ownerUserId">
                    @for (u of data.users; track u.id) {
                      <mat-option [value]="u.id">{{ u.displayName }}</mat-option>
                    }
                  </mat-select>
                  @if (group.controls.ownerUserId.hasError('required') && group.controls.ownerUserId.touched) {
                    <mat-error>Select an owner.</mat-error>
                  }
                </mat-form-field>
                <mat-form-field appearance="outline">
                  <mat-label>Target date</mat-label>
                  <input matInput [matDatepicker]="picker" formControlName="targetDate" />
                  <mat-datepicker-toggle matIconSuffix [for]="picker" />
                  <mat-datepicker #picker />
                  @if (group.controls.targetDate.hasError('required') && group.controls.targetDate.touched) {
                    <mat-error>Provide a target date.</mat-error>
                  }
                </mat-form-field>
              </div>

              <mat-form-field appearance="outline" class="full">
                <mat-label>Expected evidence type (optional)</mat-label>
                <input matInput formControlName="expectedEvidenceType" autocomplete="off" />
              </mat-form-field>
            </div>
          }
        </div>

        <button matButton type="button" (click)="addAction()">
          <mat-icon>add</mat-icon>
          Add action
        </button>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="cancel()">Cancel</button>
      <button matButton="filled" type="button" (click)="submit()" [disabled]="form.invalid">
        Submit MAP
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .actions {
      display: flex;
      flex-direction: column;
      gap: 1rem;
      min-width: 480px;
    }
    .action {
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: 12px;
      padding: 0.75rem 1rem 0.25rem;
    }
    .action__head {
      display: flex;
      align-items: center;
      justify-content: space-between;
    }
    .action__title {
      font-weight: 500;
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
      .actions {
        min-width: auto;
      }
      .row {
        flex-direction: column;
      }
    }
  `,
})
export class SubmitMapDialogComponent {
  readonly data = inject<SubmitMapDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject<
    MatDialogRef<SubmitMapDialogComponent, SubmitMapDialogResult>
  >(MatDialogRef);
  private readonly fb = inject(FormBuilder);

  readonly form = this.fb.group({
    actions: this.fb.array<ActionGroup>([this.newAction()]),
  });

  get actions(): FormArray<ActionGroup> {
    return this.form.controls.actions;
  }

  private newAction(): ActionGroup {
    return this.fb.nonNullable.group({
      description: ['', [Validators.required]],
      ownerUserId: ['', [Validators.required]],
      targetDate: [null as Date | null, [Validators.required]],
      expectedEvidenceType: [''],
    }) as unknown as ActionGroup;
  }

  addAction(): void {
    this.actions.push(this.newAction());
  }

  removeAction(index: number): void {
    if (this.actions.length > 1) {
      this.actions.removeAt(index);
    }
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const actions: MapActionInput[] = this.actions.controls.map((g) => {
      const v = g.getRawValue() as {
        description: string;
        ownerUserId: string;
        targetDate: Date | null;
        expectedEvidenceType: string;
      };
      return {
        description: v.description.trim(),
        ownerUserId: v.ownerUserId,
        targetDate: toDateOnly(v.targetDate),
        expectedEvidenceType: v.expectedEvidenceType.trim() || null,
      };
    });
    this.dialogRef.close({ actions });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
