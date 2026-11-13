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
import { RatingScalesService } from '../../../core/services/rating-scales.service';
import {
  AuditChecklistItem,
  RatingScale,
  ResponseType,
  UserDto,
} from '../../../core/models';
import {
  buildResponseConfigGroup,
  readResponseConfig,
} from '../../../core/models/response-type.util';
import { ResponseConfigFieldsComponent } from '../../../shared/components/response-config-fields/response-config-fields.component';
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

@Component({
  selector: 'app-checklist-item-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatCheckboxModule,
    MatButtonModule,
    SearchableSelectComponent,
    ResponseConfigFieldsComponent,
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

        <!-- How the item is answered: every response type, with the settings that type needs. -->
        <app-response-config-fields
          [group]="form.controls.response"
          [ratingScales]="ratingScales()"
          [scalesLoaded]="scalesLoaded()"
        />

        <app-searchable-select
          class="full"
          formControlName="sectionName"
          label="Section"
          [options]="sectionOptions"
        />

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
export class ChecklistItemDialogComponent {
  readonly data = inject<ChecklistItemDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject<
    MatDialogRef<ChecklistItemDialogComponent, ChecklistItemDialogResult>
  >(MatDialogRef);
  private readonly fb = inject(FormBuilder);
  private readonly ratingScalesService = inject(RatingScalesService);

  readonly isEdit = !!this.data.item;

  readonly ratingScales = signal<RatingScale[]>([]);
  readonly scalesLoaded = signal(false);

  readonly sectionOptions: SelectOption[] = [
    { value: '', label: 'Ungrouped' },
    ...this.data.sections.map((s) => ({ value: s, label: s })),
  ];
  readonly assigneeOptions: SelectOption[] = [
    { value: '', label: 'Unassigned' },
    ...this.data.users.map((u) => ({ value: u.id, label: u.displayName })),
  ];

  readonly form = this.fb.nonNullable.group({
    prompt: [this.data.item?.prompt ?? '', [Validators.required]],
    referenceNotes: [this.data.item?.referenceNotes ?? ''],
    // Response type + its settings (rating scale / choices / unit); the group validates per type.
    response: buildResponseConfigGroup(this.fb, this.data.item?.responseType, this.data.item?.responseConfigJson),
    sectionName: [this.data.item?.sectionName ?? ''],
    assignedUserId: [this.data.item?.assignedUserId ?? ''],
    isRequired: [this.data.item?.isRequired ?? true],
  });

  constructor() {
    // All scales, so an item that references a since-retired scale still shows it selected. A failure (e.g. no
    // template-library access) just leaves the list empty — the rating field then explains there is nothing to pick.
    this.ratingScalesService.list('all').subscribe({
      next: (scales) => {
        this.ratingScales.set(scales);
        this.scalesLoaded.set(true);
      },
      error: () => this.scalesLoaded.set(true),
    });
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    const { responseType, responseConfigJson } = readResponseConfig(this.form.controls.response);
    this.dialogRef.close({
      prompt: v.prompt.trim(),
      referenceNotes: v.referenceNotes.trim() || null,
      sectionName: v.sectionName.trim() || null,
      responseType,
      responseConfigJson,
      isRequired: v.isRequired,
      assignedUserId: v.assignedUserId || null,
    });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
