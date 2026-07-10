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

import { ControlsService } from '../../../core/services/controls.service';
import { RegulationsService } from '../../../core/services/regulations.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import {
  SearchableSelectComponent,
  SelectOption,
} from '../../../shared/components/searchable-select/searchable-select.component';

export type LinkFindingKind = 'control' | 'regulation';

export interface LinkFindingDialogData {
  kind: LinkFindingKind;
  /** Ids already linked to the finding — excluded from the picker. */
  existingIds: string[];
}

export interface LinkFindingResult {
  id: string;
}

/** Picks an active control or regulation to link to a finding (excludes already-linked items). */
@Component({
  selector: 'app-link-finding-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    TranslatePipe,
    SearchableSelectComponent,
  ],
  templateUrl: './link-finding-dialog.component.html',
})
export class LinkFindingDialogComponent {
  readonly data = inject<LinkFindingDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject<MatDialogRef<LinkFindingDialogComponent, LinkFindingResult>>(MatDialogRef);
  private readonly controls = inject(ControlsService);
  private readonly regulations = inject(RegulationsService);
  private readonly fb = inject(FormBuilder);

  readonly isControl = this.data.kind === 'control';

  private readonly allOptions = signal<SelectOption[]>([]);
  readonly options = computed<SelectOption[]>(() => {
    const excluded = new Set(this.data.existingIds);
    return this.allOptions().filter((o) => !excluded.has(o.value));
  });

  readonly form = this.fb.nonNullable.group({
    id: [null as string | null, Validators.required],
  });

  constructor() {
    if (this.isControl) {
      this.controls.list({ includeRetired: false, pageSize: 0 }).subscribe({
        next: (result) => this.allOptions.set(result.items.map((c) => ({ value: c.id, label: `${c.code} — ${c.title}` }))),
      });
    } else {
      this.regulations.list({ includeRetired: false, pageSize: 0 }).subscribe({
        next: (result) => this.allOptions.set(result.items.map((r) => ({ value: r.id, label: `${r.code} — ${r.name}` }))),
      });
    }
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    this.dialogRef.close({ id: this.form.getRawValue().id! });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
