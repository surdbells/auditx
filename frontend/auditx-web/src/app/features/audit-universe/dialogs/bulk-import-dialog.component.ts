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

import { IconComponent } from '../../../core/icons/icon.component';
import { UniverseService } from '../../../core/services/universe.service';
import { NotificationService } from '../../../core/services/notification.service';
import { BulkImportError } from '../../../core/models';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';

/** The exact header row the importer expects, in order. `name` and `entity_type` are required. */
export const ENTITY_IMPORT_COLUMNS = ['name', 'entity_type', 'parent_name', 'owner_email', 'description'] as const;

/** Quotes a CSV field when it contains a comma, a quote or a line break. */
function csvField(value: string): string {
  return /[",\r\n]/.test(value) ? `"${value.replace(/"/g, '""')}"` : value;
}

/**
 * Builds the ready-to-fill import template: the header row plus example rows that use the bank's OWN entity types
 * (so the examples import as they are), showing a parent/child pair and each optional column.
 */
export function buildEntityImportTemplate(entityTypes: string[]): string {
  const first = entityTypes[0] ?? 'branch';
  const second = entityTypes[1] ?? first;
  const rows: string[][] = [
    [...ENTITY_IMPORT_COLUMNS],
    ['Head Office', first, '', '', 'Example — a top-level entity. Replace or delete these rows.'],
    ['Lagos Branch', second, 'Head Office', '', 'Example — a child: parent_name must match an entity name exactly.'],
    ['Treasury Operations', first, 'Head Office', 'owner@yourbank.com', 'Example — owner_email must be an existing AuditX user.'],
  ];
  return rows.map((r) => r.map(csvField).join(',')).join('\r\n') + '\r\n';
}

/**
 * Bulk import of audit-universe entities from CSV: download the template, fill it in a spreadsheet, then choose the
 * file (or paste its contents). The import is atomic — on any row error nothing is created, the dialog stays open and
 * lists each problem by row so the sheet can be fixed and re-submitted.
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
    IconComponent,
    TranslatePipe,
  ],
  template: `
    <h2 mat-dialog-title>{{ 'universe.bulk.title' | t }}</h2>
    <mat-dialog-content>
      <p class="hint">{{ 'universe.bulk.hint' | t }}</p>

      <!-- What each column means — the template carries the same header. -->
      <dl class="columns">
        <div><dt><code>name</code></dt><dd>{{ 'universe.bulk.col.name' | t }}</dd></div>
        <div>
          <dt><code>entity_type</code></dt>
          <dd>
            {{ 'universe.bulk.col.entityType' | t }}
            @if (entityTypes().length) {
              <span class="types">
                @for (type of entityTypes(); track type) {
                  <code>{{ type }}</code>
                }
              </span>
            }
          </dd>
        </div>
        <div><dt><code>parent_name</code></dt><dd>{{ 'universe.bulk.col.parentName' | t }}</dd></div>
        <div><dt><code>owner_email</code></dt><dd>{{ 'universe.bulk.col.ownerEmail' | t }}</dd></div>
        <div><dt><code>description</code></dt><dd>{{ 'universe.bulk.col.description' | t }}</dd></div>
      </dl>

      <div class="upload">
        <button matButton="outlined" type="button" (click)="downloadTemplate()">
          <app-icon name="download" />
          {{ 'universe.bulk.downloadTemplate' | t }}
        </button>
        <button matButton="outlined" type="button" (click)="fileInput.click()">
          <app-icon name="upload_file" />
          {{ 'universe.bulk.chooseFile' | t }}
        </button>
        <input #fileInput type="file" accept=".csv,text/csv" hidden (change)="onFileSelected($event)" />
        @if (fileName(); as name) {
          <span class="file" role="status">{{ 'universe.bulk.fileLoaded' | t: { name: name, rows: rowCount() } }}</span>
        }
      </div>

      <form [formGroup]="form" class="form">
        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ 'universe.bulk.csvLabel' | t }}</mat-label>
          <textarea
            matInput
            formControlName="csvContent"
            rows="7"
            spellcheck="false"
            placeholder="name,entity_type,parent_name,owner_email,description&#10;Head Office,branch,,,&#10;Lagos Branch,branch,Head Office,,"
            autocomplete="off"
          ></textarea>
          @if (form.controls.csvContent.hasError('required') && form.controls.csvContent.touched) {
            <mat-error>{{ 'universe.bulk.csvRequired' | t }}</mat-error>
          }
        </mat-form-field>
      </form>

      @if (errors().length) {
        <div class="errors" role="alert">
          <h3 class="errors__title">{{ 'universe.bulk.rowErrors' | t: { count: errors().length } }}</h3>
          <table mat-table [dataSource]="errors()" class="errors__table">
            <ng-container matColumnDef="row">
              <th mat-header-cell *matHeaderCellDef>{{ 'universe.bulk.colRow' | t }}</th>
              <td mat-cell *matCellDef="let e">{{ e.row }}</td>
            </ng-container>
            <ng-container matColumnDef="field">
              <th mat-header-cell *matHeaderCellDef>{{ 'universe.bulk.colField' | t }}</th>
              <td mat-cell *matCellDef="let e"><code>{{ e.field }}</code></td>
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
      min-width: min(560px, 82vw);
    }
    .full {
      width: 100%;
    }
    .hint {
      color: var(--mat-sys-on-surface-variant);
      margin-top: 0;
    }
    .columns {
      display: grid;
      gap: 0.35rem;
      margin: 0 0 1rem;
      padding: 0.75rem 0.9rem;
      border: 1px solid var(--mat-sys-outline-variant);
      border-radius: 12px;
      background: var(--mat-sys-surface-container-low);
      font: var(--mat-sys-body-small);
    }
    .columns div {
      display: grid;
      grid-template-columns: 7.5rem 1fr;
      gap: 0.75rem;
    }
    .columns dt,
    .columns dd {
      margin: 0;
    }
    .columns dd {
      color: var(--mat-sys-on-surface-variant);
    }
    code {
      font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
      font-size: 0.92em;
      color: var(--mat-sys-on-surface);
    }
    .types {
      display: flex;
      flex-wrap: wrap;
      gap: 0.25rem 0.4rem;
      margin-top: 0.25rem;
    }
    .types code {
      padding: 0.05rem 0.4rem;
      border-radius: 6px;
      background: var(--mat-sys-surface-container-highest);
    }
    .upload {
      display: flex;
      flex-wrap: wrap;
      align-items: center;
      gap: 0.5rem;
      margin-bottom: 1rem;
    }
    .file {
      font: var(--mat-sys-body-small);
      color: var(--mat-sys-on-surface-variant);
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
      .columns div {
        grid-template-columns: 1fr;
        gap: 0;
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

  /** The bank's active entity types — the only values `entity_type` accepts; also used in the template's examples. */
  readonly entityTypes = signal<string[]>([]);

  /** Name of the file last loaded into the box, and how many data rows it carried. */
  readonly fileName = signal<string | null>(null);
  readonly rowCount = signal(0);

  constructor() {
    this.universe.entityTypes().subscribe({
      next: (types) => this.entityTypes.set(types),
      // Non-fatal: the template falls back to a generic example and the server still validates.
      error: () => this.entityTypes.set([]),
    });
  }

  /** Download a ready-to-fill CSV with the exact header and examples that use this bank's entity types. */
  downloadTemplate(): void {
    // The byte-order mark makes Excel open the file as UTF-8 (accented names survive the round trip).
    const blob = new Blob(['﻿' + buildEntityImportTemplate(this.entityTypes())], { type: 'text/csv;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = 'auditx-entities-template.csv';
    link.click();
    URL.revokeObjectURL(url);
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) {
      return;
    }
    const reader = new FileReader();
    reader.onload = () => {
      const content = String(reader.result ?? '');
      this.form.controls.csvContent.setValue(content);
      this.errors.set([]);
      this.fileName.set(file.name);
      // Data rows = non-empty lines minus the header.
      this.rowCount.set(Math.max(0, content.split(/\r?\n/).filter((line) => line.trim().length > 0).length - 1));
    };
    reader.readAsText(file);
    // Reset so choosing the same (corrected) file again still fires a change event.
    input.value = '';
  }

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
