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

export interface SectionNameDialogData {
  title: string;
  label: string;
  confirmLabel: string;
  cancelLabel: string;
  duplicateError: string;
  requiredError: string;
  initialName?: string;
  /** Existing section names (case-insensitive) used to block duplicates. */
  existingNames: string[];
}

export interface SectionNameDialogResult {
  name: string;
}

/** Minimal single-field dialog for adding or renaming a checklist section. Strings are supplied (translated) by the caller. */
@Component({
  selector: 'app-section-name-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
  ],
  template: `
    <h2 mat-dialog-title>{{ data.title }}</h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="form" (ngSubmit)="submit()">
        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ data.label }}</mat-label>
          <input matInput formControlName="name" autocomplete="off" cdkFocusInitial />
          @if (form.controls.name.hasError('required') && form.controls.name.touched) {
            <mat-error>{{ data.requiredError }}</mat-error>
          }
          @if (form.controls.name.hasError('duplicate')) {
            <mat-error>{{ data.duplicateError }}</mat-error>
          }
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="cancel()">{{ data.cancelLabel }}</button>
      <button matButton="filled" type="button" (click)="submit()" [disabled]="form.invalid">
        {{ data.confirmLabel }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .form {
      display: flex;
      flex-direction: column;
      min-width: 360px;
    }
    .full {
      width: 100%;
    }
    @media (max-width: 420px) {
      .form {
        min-width: auto;
      }
    }
  `,
})
export class SectionNameDialogComponent {
  readonly data = inject<SectionNameDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject<
    MatDialogRef<SectionNameDialogComponent, SectionNameDialogResult>
  >(MatDialogRef);
  private readonly fb = inject(FormBuilder);

  private readonly taken = new Set(
    this.data.existingNames
      .filter((n) => n.toLowerCase() !== (this.data.initialName ?? '').toLowerCase())
      .map((n) => n.toLowerCase()),
  );

  readonly form = this.fb.nonNullable.group({
    name: [
      this.data.initialName ?? '',
      [Validators.required, this.notDuplicate.bind(this)],
    ],
  });

  private notDuplicate(control: AbstractControl): ValidationErrors | null {
    const value = (control.value as string)?.trim().toLowerCase();
    return value && this.taken.has(value) ? { duplicate: true } : null;
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.dialogRef.close({ name: this.form.controls.name.value.trim() });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
