import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import {
  MAT_DIALOG_DATA,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';

import { UniverseService } from '../../../core/services/universe.service';
import { UserLookupService } from '../../../core/services/user-lookup.service';
import { OrgUnitLookupService } from '../../../core/services/org-unit-lookup.service';
import { RiskDimensionsService } from '../../../core/services/risk-dimensions.service';
import { NotificationService } from '../../../core/services/notification.service';
import { AuthService } from '../../../core/services/auth.service';
import { Permissions } from '../../../core/permissions';
import {
  AuditTrailEntryView,
  Entity,
  RiskDimension,
} from '../../../core/models';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';
import { SearchableSelectComponent } from '../../../shared/components/searchable-select/searchable-select.component';

export interface EntityEditorDialogData {
  /** Present when editing; absent for create. */
  entity?: Entity;
  /** Entity-type options sourced from `/entity-types`. */
  entityTypes: string[];
  /** Other entities that can be selected as a parent (excludes self). */
  parentCandidates: Entity[];
}

interface ScoreRow {
  dimension: RiskDimension;
  inherent: number | null;
  residual: number | null;
}

/**
 * Create/edit an audit-universe entity and (when editing) score its risk.
 *
 * Metadata changes go through PATCH carrying the entity `version` so the backend
 * can detect concurrent edits (409). Risk scores are saved separately through
 * the risk-scores endpoint — each submitted side (inherent/residual) must score
 * every active dimension, and the request carries the current version.
 */
@Component({
  selector: 'app-entity-editor-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatTableModule,
    MatExpansionModule,
    MatProgressSpinnerModule,
    TranslatePipe,
    SearchableSelectComponent,
  ],
  templateUrl: './entity-editor-dialog.component.html',
  styleUrl: './entity-editor-dialog.component.scss',
})
export class EntityEditorDialogComponent {
  readonly data = inject<EntityEditorDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef =
    inject<MatDialogRef<EntityEditorDialogComponent, boolean>>(MatDialogRef);
  private readonly fb = inject(FormBuilder);
  private readonly universe = inject(UniverseService);
  /** Populates the owner select (lazy directory load). */
  readonly userLookup = inject(UserLookupService);
  /** Populates the org-unit picker (lazy tree load). */
  readonly orgUnitLookup = inject(OrgUnitLookupService);
  private readonly riskDimensions = inject(RiskDimensionsService);
  private readonly notify = inject(NotificationService);
  private readonly auth = inject(AuthService);
  private readonly i18n = inject(TranslationService);

  readonly isEdit = !!this.data.entity;

  /** Tracks the current persisted entity (refreshed after each save). */
  readonly entity = signal<Entity | null>(this.data.entity ?? null);

  readonly canManage = computed(() =>
    this.auth.hasPermission(Permissions.ManageUniverse),
  );
  readonly canScore = computed(() =>
    this.auth.hasPermission(Permissions.ScoreRisk),
  );

  readonly form = this.fb.nonNullable.group({
    name: [
      this.data.entity?.name ?? '',
      [Validators.required, Validators.maxLength(200)],
    ],
    entityType: [
      this.data.entity?.entityType ?? '',
      [Validators.required],
    ],
    description: [this.data.entity?.description ?? ''],
    parentEntityId: [this.data.entity?.parentEntityId ?? ''],
    ownerUserId: [this.data.entity?.ownerUserId ?? ''],
    orgUnitId: [this.data.entity?.orgUnitId ?? null as string | null],
  });

  readonly savingMeta = signal(false);
  readonly savingScores = signal(false);

  /** Active dimensions for scoring (loaded only in edit mode). */
  readonly scoreRows = signal<ScoreRow[]>([]);
  readonly scoresLoading = signal(false);
  readonly scoreColumns = ['dimension', 'scale', 'inherent', 'residual'];

  readonly history = signal<AuditTrailEntryView[]>([]);
  readonly historyLoading = signal(false);
  readonly historyLoaded = signal(false);

  readonly composite = computed(() => {
    const e = this.entity();
    return {
      inherent: e?.compositeInherentScore ?? null,
      residual: e?.compositeResidualScore ?? null,
    };
  });

  constructor() {
    this.userLookup.ensureLoaded();
    this.orgUnitLookup.ensureLoaded();
    if (this.isEdit) {
      this.loadScoreRows();
    }
  }

  private loadScoreRows(): void {
    const e = this.entity();
    if (!e) {
      return;
    }
    this.scoresLoading.set(true);
    this.riskDimensions.list('true').subscribe({
      next: (dimensions) => {
        const rows: ScoreRow[] = dimensions.map((d) => ({
          dimension: d,
          inherent: e.inherentScores[d.name] ?? null,
          residual: e.residualScores[d.name] ?? null,
        }));
        this.scoreRows.set(rows);
        this.scoresLoading.set(false);
      },
      error: () => this.scoresLoading.set(false),
    });
  }

  /** Constrains a typed score into the dimension's [min,max] (or clears it). */
  setScore(
    index: number,
    side: 'inherent' | 'residual',
    raw: string,
  ): void {
    this.scoreRows.update((rows) => {
      const row = rows[index];
      if (!row) {
        return rows;
      }
      const next = [...rows];
      let value: number | null = raw === '' ? null : Number(raw);
      if (value !== null && !Number.isNaN(value)) {
        value = Math.min(
          Math.max(value, row.dimension.scaleMin),
          row.dimension.scaleMax,
        );
      } else {
        value = null;
      }
      next[index] = { ...row, [side]: value };
      return next;
    });
  }

  saveMeta(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    this.savingMeta.set(true);

    const onError = (err: unknown): void => {
      this.savingMeta.set(false);
      if (err instanceof HttpErrorResponse && err.status === 409) {
        this.notify.error(this.i18n.translate('universe.error.conflictEntity'));
      }
      // Other errors already surfaced by the global interceptor.
    };

    if (this.isEdit) {
      const current = this.entity();
      if (!current) {
        return;
      }
      this.universe
        .update(current.id, {
          name: v.name.trim(),
          entityType: v.entityType,
          description: v.description.trim() || null,
          ownerUserId: v.ownerUserId.trim() || null,
          parentEntityId: v.parentEntityId || null,
          orgUnitId: v.orgUnitId || null,
          version: current.version,
        })
        .subscribe({
          next: (updated) => {
            this.savingMeta.set(false);
            this.entity.set(updated);
            this.notify.success(
              this.i18n.translate('universe.notify.saved', {
                name: updated.name,
              }),
            );
          },
          error: onError,
        });
    } else {
      this.universe
        .create({
          name: v.name.trim(),
          entityType: v.entityType,
          description: v.description.trim() || null,
          parentEntityId: v.parentEntityId || null,
          ownerUserId: v.ownerUserId.trim() || null,
          orgUnitId: v.orgUnitId || null,
        })
        .subscribe({
          next: (created) => {
            this.savingMeta.set(false);
            this.entity.set(created);
            this.notify.success(
              this.i18n.translate('universe.notify.created', {
                name: created.name,
              }),
            );
            // Switch into edit mode so scoring becomes available.
            this.dialogRef.close(true);
          },
          error: onError,
        });
    }
  }

  /**
   * Saves whichever side(s) the user has fully scored. A side is submitted only
   * when EVERY active dimension on it has a value; a partially filled side is
   * rejected client-side with guidance.
   */
  saveScores(): void {
    const current = this.entity();
    if (!current) {
      return;
    }
    const rows = this.scoreRows();
    if (!rows.length) {
      return;
    }

    const inherentComplete = rows.every((r) => r.inherent !== null);
    const residualComplete = rows.every((r) => r.residual !== null);
    const inherentAny = rows.some((r) => r.inherent !== null);
    const residualAny = rows.some((r) => r.residual !== null);

    if (inherentAny && !inherentComplete) {
      this.notify.warning(
        this.i18n.translate('universe.warn.inherentIncomplete'),
      );
      return;
    }
    if (residualAny && !residualComplete) {
      this.notify.warning(
        this.i18n.translate('universe.warn.residualIncomplete'),
      );
      return;
    }
    if (!inherentComplete && !residualComplete) {
      this.notify.warning(this.i18n.translate('universe.warn.scoringRequired'));
      return;
    }

    const body: {
      inherentScores?: Record<string, number>;
      residualScores?: Record<string, number>;
      version: number;
    } = { version: current.version };

    if (inherentComplete) {
      body.inherentScores = Object.fromEntries(
        rows.map((r) => [r.dimension.name, r.inherent as number]),
      );
    }
    if (residualComplete) {
      body.residualScores = Object.fromEntries(
        rows.map((r) => [r.dimension.name, r.residual as number]),
      );
    }

    this.savingScores.set(true);
    this.universe.saveRiskScores(current.id, body).subscribe({
      next: (updated) => {
        this.savingScores.set(false);
        this.entity.set(updated);
        this.loadScoreRows();
        this.historyLoaded.set(false);
        this.notify.success(this.i18n.translate('universe.notify.scoresSaved'));
      },
      error: (err: unknown) => {
        this.savingScores.set(false);
        if (err instanceof HttpErrorResponse && err.status === 409) {
          this.notify.error(this.i18n.translate('universe.error.conflictScores'));
        }
      },
    });
  }

  loadHistory(): void {
    const current = this.entity();
    if (!current || this.historyLoaded()) {
      return;
    }
    this.historyLoading.set(true);
    this.universe.riskScoreHistory(current.id).subscribe({
      next: (entries) => {
        this.history.set(entries);
        this.historyLoading.set(false);
        this.historyLoaded.set(true);
      },
      error: () => this.historyLoading.set(false),
    });
  }

  close(): void {
    this.dialogRef.close(this.entity() !== this.data.entity);
  }
}
