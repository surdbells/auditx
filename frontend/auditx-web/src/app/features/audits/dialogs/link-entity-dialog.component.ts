import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';

import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { EntityLookupService } from '../../../core/services/entity-lookup.service';
import {
  SearchableSelectComponent,
  SelectOption,
} from '../../../shared/components/searchable-select/searchable-select.component';

export interface LinkEntityDialogData {
  /** The entity the audit is linked to now, if any. */
  currentEntityId: string | null;
  /** A completed audit already counts toward coverage — say that moving it moves the credit. */
  isCompleted: boolean;
}

/** What the dialog closes with: the chosen entity, or null to clear the link. */
export interface LinkEntityDialogResult {
  entityId: string | null;
}

/**
 * Picks the audit-universe entity an ad-hoc audit examines. The link is what makes the audit count toward that
 * entity's coverage ("last audited", the coverage matrix, the entity's audit history).
 */
@Component({
  selector: 'app-link-entity-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [ReactiveFormsModule, MatDialogModule, MatButtonModule, SearchableSelectComponent, TranslatePipe],
  template: `
    <h2 mat-dialog-title>{{ 'audits.entity.dialogTitle' | t }}</h2>
    <mat-dialog-content class="link">
      <p class="link__hint">
        {{ (data.isCompleted ? 'audits.entity.dialogHintCompleted' : 'audits.entity.dialogHint') | t }}
      </p>
      <app-searchable-select
        class="link__field"
        [formControl]="entity"
        [label]="'audits.field.entity' | t"
        [options]="options()"
        [searchPlaceholder]="'audits.entity.search' | t"
        [noResults]="'audits.entity.noResults' | t"
        [clearable]="true"
        [clearLabel]="'audits.entity.none' | t"
      />
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" mat-dialog-close>{{ 'common.cancel' | t }}</button>
      <button matButton="filled" type="button" (click)="save()" [disabled]="!changed()">
        {{ 'common.save' | t }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .link {
      display: flex;
      flex-direction: column;
      min-width: min(460px, 80vw);
    }
    .link__hint {
      margin: 0 0 1rem;
      color: var(--mat-sys-on-surface-variant);
    }
    .link__field {
      width: 100%;
    }
  `,
})
export class LinkEntityDialogComponent {
  readonly data = inject<LinkEntityDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef =
    inject<MatDialogRef<LinkEntityDialogComponent, LinkEntityDialogResult>>(MatDialogRef);
  private readonly entityLookup = inject(EntityLookupService);

  readonly entity = new FormControl<string | null>(this.data.currentEntityId);

  /** Live (not archived) entities only — an archived entity cannot be newly linked. */
  readonly options = computed<SelectOption[]>(() =>
    this.entityLookup
      .options()
      .filter((e) => !e.isArchived)
      .map((e) => ({ value: e.id, label: e.name })),
  );

  constructor() {
    this.entityLookup.ensureLoaded();
  }

  changed(): boolean {
    return (this.entity.value || null) !== (this.data.currentEntityId || null);
  }

  save(): void {
    this.dialogRef.close({ entityId: this.entity.value || null });
  }
}
