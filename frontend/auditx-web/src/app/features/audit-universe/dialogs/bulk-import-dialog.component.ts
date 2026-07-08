import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import {
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatTableModule } from '@angular/material/table';

import { UniverseService } from '../../../core/services/universe.service';
import { NotificationService } from '../../../core/services/notification.service';
import { BulkImportError } from '../../../core/models';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';

/**
 * Pastes CSV content and submits it for atomic bulk import. On success the
 * dialog closes with the created count; on per-row validation errors it stays
 * open and renders the error rows so the user can fix and retry.
 */
@Component({
  selector: 'app-bulk-import-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatTableModule,
    TranslatePipe,
  ],
  template: `
    <h2 mat-dialog-title>{{ 'universe.bulk.title' | t }}</h2>
    <mat-dialog-content>
      <p class="hint">
        {{ 'universe.bulk.hint' | t }}
      </p>
      <form [formGroup]="form" class="form">
        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ 'universe.bulk.csvLabel' | t }}</mat-label>
          <textarea
            matInput
            formControlName="csvContent"
            rows="8"
            placeholder="name,entityType,description"
            autocomplete="off"
          ></textarea>
          @if (form.controls.csvContent.hasError('required') && form.controls.csvContent.touched) {
            <mat-error>{{ 'universe.bulk.csvRequired' | t }}</mat-error>
          }
        </mat-form-field>
      </form>

      @if (errors().length) {
        <div class="errors">
          <h3 class="errors__title">{{ 'universe.bulk.rowErrors' | t: { count: errors().length } }}</h3>
          <table mat-table [dataSource]="errors()" class="errors__table">
            <ng-container matColumnDef="row">
              <th mat-header-cell *matHeaderCellDef>{{ 'universe.bulk.colRow' | t }}</th>
              <td mat-cell *matCellDef="let e">{{ e.row }}</td>
            </ng-container>
            <ng-container matColumnDef="field">
              <th mat-header-cell *matHeaderCellDef>{{ 'universe.bulk.colField' | t }}</th>
              <td mat-cell *matCellDef="let e">{{ e.field }}</td>
            </ng-container>
            <ng-container matColumnDef="message">
              <th mat-header-cell *matHeaderCellDef>{{ 'universe.bulk.colMessage' | t }}</th>
              <td mat-cell *matCellDef="let e">{{ e.message }}</td>
            </ng-container>
            <tr mat-header-row *matHeaderRowDef="errorColumns"></tr>
            <tr mat-row *matRowDef="let row; columns: errorColumns"></tr>
          </table>
        </div>
      }
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="cancel()">{{ 'universe.actions.cancel' | t }}</button>
      <button
        matButton="filled"
        type="button"
        (click)="submit()"
        [disabled]="form.invalid || submitting()"
      >
        {{ submitting() ? ('universe.actions.importing' | t) : ('universe.actions.import' | t) }}
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
    .hint {
      color: var(--mat-sys-on-surface-variant);
      margin-top: 0;
    }
    .errors {
      margin-top: 0.5rem;
    }
    .errors__title {
      font: var(--mat-sys-title-small);
      color: var(--mat-sys-error);
      margin: 0 0 0.5rem;
    }
    .errors__table {
      width: 100%;
    }
    @media (max-width: 560px) {
      .form {
        min-width: auto;
      }
    }
  `,
})
export class BulkImportDialogComponent {
  private readonly dialogRef =
    inject<MatDialogRef<BulkImportDialogComponent, number>>(MatDialogRef);
  private readonly fb = inject(FormBuilder);
  private readonly universe = inject(UniverseService);
  private readonly notify = inject(NotificationService);
  private readonly i18n = inject(TranslationService);

  readonly errorColumns = ['row', 'field', 'message'];

  readonly form = this.fb.nonNullable.group({
    csvContent: ['', [Validators.required]],
  });

  readonly errors = signal<BulkImportError[]>([]);
  readonly submitting = signal(false);

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.errors.set([]);
    this.submitting.set(true);
    this.universe
      .bulkImport({ csvContent: this.form.getRawValue().csvContent })
      .subscribe({
        next: (result) => {
          this.submitting.set(false);
          if (result.errors.length) {
            this.errors.set(result.errors);
            this.notify.warning(
              this.i18n.translate('universe.warn.importRejected', {
                count: result.errors.length,
              }),
            );
            return;
          }
          this.dialogRef.close(result.created);
        },
        error: () => this.submitting.set(false),
      });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
