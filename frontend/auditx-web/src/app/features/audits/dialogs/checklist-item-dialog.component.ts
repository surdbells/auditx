import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import {
  MAT_DIALOG_DATA,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { SelectAllDirective } from '../../../shared/directives/select-all.directive';

import {
  AuditChecklistItem,
  ResponseType,
  UserDto,
} from '../../../core/models';
import {
  SearchableSelectComponent,
  SelectOption,
} from '../../../shared/components/searchable-select/searchable-select.component';

export interface ChecklistItemDialogData {
  /** Present when editing an existing item. */
  item?: AuditChecklistItem;
  /** Users selectable as the assignee. */
  users: UserDto[];
  /** Existing section names the item can be filed under (managed via the Add Section control). */
  sections: string[];
}

export interface ChecklistItemDialogResult {
  prompt: string;
  referenceNotes: string | null;
  sectionName: string | null;
  responseType: ResponseType;
  responseConfigJson: string | null;
  isRequired: boolean;
  assignedUserId: string | null;
}

interface TypeOption {
  value: ResponseType;
  label: string;
}

const TYPE_OPTIONS: TypeOption[] = [
  { value: 'pass_fail_na', label: 'Pass / Fail / N/A' },
  { value: 'yes_no', label: 'Yes / No' },
  { value: 'text', label: 'Free text' },
  { value: 'numeric', label: 'Numeric' },
  { value: 'date', label: 'Date' },
  { value: 'rating', label: 'Rating (scale)' },
  { value: 'multiple_choice', label: 'Multiple choice' },
];

@Component({
  selector: 'app-checklist-item-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    SelectAllDirective,
    MatCheckboxModule,
    MatButtonModule,
    SearchableSelectComponent,
  ],
  template: `
    <h2 mat-dialog-title>{{ isEdit ? 'Edit checklist item' : 'Add checklist item' }}</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="form">
        <mat-form-field appearance="outline" class="full">
          <mat-label>Prompt</mat-label>
          <textarea matInput formControlName="prompt" rows="2"></textarea>
          @if (form.controls.prompt.hasError('required') && form.controls.prompt.touched) {
            <mat-error>A prompt is required.</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline" class="full">
          <mat-label>Reference notes</mat-label>
          <textarea matInput formControlName="referenceNotes" rows="2"></textarea>
        </mat-form-field>

        <div class="row">
          <app-searchable-select
            formControlName="sectionName"
            label="Section"
            [options]="sectionOptions"
          />
          <mat-form-field appearance="outline">
            <mat-label>Response type</mat-label>
            <mat-select
              formControlName="responseType"
              (selectionChange)="currentType.set($event.value)"
            >
              @for (t of typeOptions; track t.value) {
                <mat-option [value]="t.value">{{ t.label }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
        </div>

        @if (currentType() === 'multiple_choice') {
          <mat-form-field appearance="outline" class="full">
            <mat-label>Choices (one per line)</mat-label>
            <textarea matInput formControlName="choices" rows="3" placeholder="Option A&#10;Option B"></textarea>
            @if (form.controls.choices.hasError('required') && form.controls.choices.touched) {
              <mat-error>Enter at least two choices.</mat-error>
            }
          </mat-form-field>
        }
        @if (currentType() === 'rating') {
          <mat-form-field appearance="outline">
            <mat-label>Maximum rating</mat-label>
            <input matInput type="number" formControlName="ratingMax" min="2" max="10" />
          </mat-form-field>
        }
        @if (currentType() === 'numeric') {
          <mat-form-field appearance="outline">
            <mat-label>Unit (optional)</mat-label>
            <input matInput formControlName="unit" autocomplete="off" placeholder="e.g. days, %, USD" />
          </mat-form-field>
        }

        <app-searchable-select
          class="full"
          formControlName="assignedUserId"
          label="Assignee"
          [options]="assigneeOptions"
        />

        <mat-checkbox formControlName="isRequired">Required</mat-checkbox>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="cancel()">Cancel</button>
      <button matButton="filled" type="button" (click)="submit()" [disabled]="form.invalid">
        {{ isEdit ? 'Save' : 'Add item' }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .form {
      display: flex;
      flex-direction: column;
      min-width: 460px;
      gap: 0.25rem;
    }
    .row {
      display: flex;
      gap: 1rem;
    }
    .row mat-form-field,
    .row app-searchable-select {
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
export class ChecklistItemDialogComponent {
  readonly data = inject<ChecklistItemDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject<
    MatDialogRef<ChecklistItemDialogComponent, ChecklistItemDialogResult>
  >(MatDialogRef);
  private readonly fb = inject(FormBuilder);

  readonly isEdit = !!this.data.item;
  readonly typeOptions = TYPE_OPTIONS;

  readonly sectionOptions: SelectOption[] = [
    { value: '', label: 'Ungrouped' },
    ...this.data.sections.map((s) => ({ value: s, label: s })),
  ];
  readonly assigneeOptions: SelectOption[] = [
    { value: '', label: 'Unassigned' },
    ...this.data.users.map((u) => ({ value: u.id, label: u.displayName })),
  ];

  private readonly config = parseConfig(this.data.item?.responseConfigJson ?? null);
  readonly currentType = signal<ResponseType>(this.data.item?.responseType ?? 'pass_fail_na');

  readonly form = this.fb.nonNullable.group({
    prompt: [this.data.item?.prompt ?? '', [Validators.required]],
    referenceNotes: [this.data.item?.referenceNotes ?? ''],
    sectionName: [this.data.item?.sectionName ?? ''],
    responseType: [
      (this.data.item?.responseType ?? 'pass_fail_na') as ResponseType,
      [Validators.required],
    ],
    choices: [(this.config.options ?? []).join('\n')],
    ratingMax: [this.config.max ?? 5],
    unit: [this.config.unit ?? ''],
    assignedUserId: [this.data.item?.assignedUserId ?? ''],
    isRequired: [this.data.item?.isRequired ?? true],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    this.dialogRef.close({
      prompt: v.prompt.trim(),
      referenceNotes: v.referenceNotes.trim() || null,
      sectionName: v.sectionName.trim() || null,
      responseType: v.responseType,
      responseConfigJson: this.buildConfig(v.responseType, v.choices, v.ratingMax, v.unit),
      isRequired: v.isRequired,
      assignedUserId: v.assignedUserId || null,
    });
  }

  private buildConfig(
    type: ResponseType,
    choices: string,
    ratingMax: number,
    unit: string,
  ): string | null {
    if (type === 'multiple_choice') {
      const options = choices
        .split('\n')
        .map((o) => o.trim())
        .filter((o) => o.length > 0);
      return options.length ? JSON.stringify({ options }) : null;
    }
    if (type === 'rating') {
      const max = Math.min(10, Math.max(2, Math.round(ratingMax || 5)));
      return JSON.stringify({ max });
    }
    if (type === 'numeric' && unit.trim()) {
      return JSON.stringify({ unit: unit.trim() });
    }
    return null;
  }

  cancel(): void {
    this.dialogRef.close();
  }
}

interface ParsedConfig {
  options?: string[];
  max?: number;
  unit?: string;
}

function parseConfig(json: string | null): ParsedConfig {
  if (!json) {
    return {};
  }
  try {
    return JSON.parse(json) as ParsedConfig;
  } catch {
    return {};
  }
}
