import {
  ChangeDetectionStrategy,
  Component,
  inject,
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

import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import {
  ResponseType,
  SaveTemplateItemRequest,
  TemplateItem,
} from '../../../../core/models';

export interface ItemEditorDialogData {
  /** Existing item to edit, or null to create a new one. */
  item: TemplateItem | null;
  /** Section names available to assign the item to. */
  sections: string[];
}

const RESPONSE_TYPES: { value: ResponseType; labelKey: string }[] = [
  { value: 'pass_fail_na', labelKey: 'templatesAdmin.responseType.passFailNa' },
];

@Component({
  selector: 'app-item-editor-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatCheckboxModule,
    MatButtonModule,
    TranslatePipe,
  ],
  template: `
    <h2 mat-dialog-title>{{ (data.item ? 'templatesAdmin.editor.editItem' : 'templatesAdmin.editor.addItem') | t }}</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="form">
        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ 'templatesAdmin.item.prompt' | t }}</mat-label>
          <textarea
            matInput
            formControlName="prompt"
            rows="2"
            cdkFocusInitial
          ></textarea>
          @if (form.controls.prompt.hasError('required') && form.controls.prompt.touched) {
            <mat-error>{{ 'templatesAdmin.error.promptRequired' | t }}</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ 'templatesAdmin.item.referenceNotes' | t }}</mat-label>
          <textarea matInput formControlName="referenceNotes" rows="2"></textarea>
        </mat-form-field>

        <div class="row">
          <mat-form-field appearance="outline">
            <mat-label>{{ 'templatesAdmin.item.responseType' | t }}</mat-label>
            <mat-select formControlName="responseType">
              @for (rt of responseTypes; track rt.value) {
                <mat-option [value]="rt.value">{{ rt.label }}</mat-option>
              }
            </mat-select>
          </mat-form-field>

          <mat-form-field appearance="outline">
            <mat-label>{{ 'templatesAdmin.item.section' | t }}</mat-label>
            <mat-select formControlName="sectionName">
              <mat-option [value]="''">{{ 'templatesAdmin.item.noSection' | t }}</mat-option>
              @for (s of data.sections; track s) {
                <mat-option [value]="s">{{ s }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
        </div>

        <mat-checkbox formControlName="isRequired">{{ 'templatesAdmin.editor.required' | t }}</mat-checkbox>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="cancel()">{{ 'templatesAdmin.action.cancel' | t }}</button>
      <button matButton="filled" type="button" (click)="submit()" [disabled]="form.invalid">
        {{ (data.item ? 'templatesAdmin.action.save' : 'templatesAdmin.action.add') | t }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .form {
      display: flex;
      flex-direction: column;
      gap: 0.25rem;
      min-width: 420px;
    }
    .full {
      width: 100%;
    }
    .row {
      display: flex;
      gap: 1rem;
      flex-wrap: wrap;
    }
    .row mat-form-field {
      flex: 1 1 180px;
    }
    @media (max-width: 520px) {
      .form {
        min-width: auto;
      }
    }
  `,
})
export class ItemEditorDialogComponent {
  readonly data = inject<ItemEditorDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef =
    inject<MatDialogRef<ItemEditorDialogComponent, SaveTemplateItemRequest>>(
      MatDialogRef,
    );
  private readonly fb = inject(FormBuilder);
  private readonly i18n = inject(TranslationService);

  readonly responseTypes = RESPONSE_TYPES.map((rt) => ({
    value: rt.value,
    label: this.i18n.translate(rt.labelKey),
  }));

  readonly form = this.fb.nonNullable.group({
    prompt: [this.data.item?.prompt ?? '', [Validators.required]],
    referenceNotes: [this.data.item?.referenceNotes ?? ''],
    responseType: [
      (this.data.item?.responseType ?? 'pass_fail_na') as ResponseType,
      [Validators.required],
    ],
    sectionName: [this.data.item?.sectionName ?? ''],
    isRequired: [this.data.item?.isRequired ?? false],
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    const result: SaveTemplateItemRequest = {
      prompt: v.prompt.trim(),
      referenceNotes: v.referenceNotes.trim(),
      responseType: v.responseType,
      sectionName: v.sectionName,
      isRequired: v.isRequired,
      defaultAssignmentRuleJson:
        this.data.item?.defaultAssignmentRuleJson ?? null,
    };
    this.dialogRef.close(result);
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
