import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
} from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import {
  MAT_DIALOG_DATA,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';

import {
  RegisterRiskRequest,
  Risk,
  RISK_STRATEGIES,
  RiskBand,
  RiskTreatmentStrategy,
  UpdateRiskRequest,
} from '../../../core/models';
import { UserLookupService } from '../../../core/services/user-lookup.service';
import { EntityLookupService } from '../../../core/services/entity-lookup.service';
import { NotificationService } from '../../../core/services/notification.service';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';
import {
  SearchableSelectComponent,
  SelectOption,
} from '../../../shared/components/searchable-select/searchable-select.component';
import { bandOf } from '../risk-format';

export interface RiskEditorDialogData {
  risk?: Risk;
}

export type RiskEditorResult =
  | { mode: 'register'; body: RegisterRiskRequest }
  | { mode: 'update'; id: string; body: UpdateRiskRequest };

@Component({
  selector: 'app-risk-editor-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    TranslatePipe,
    SearchableSelectComponent,
  ],
  templateUrl: './risk-editor-dialog.component.html',
  styleUrl: './risk-editor-dialog.component.scss',
})
export class RiskEditorDialogComponent {
  readonly data = inject<RiskEditorDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef = inject<MatDialogRef<RiskEditorDialogComponent, RiskEditorResult>>(MatDialogRef);
  private readonly fb = inject(FormBuilder);
  readonly userLookup = inject(UserLookupService);
  readonly entityLookup = inject(EntityLookupService);
  private readonly notify = inject(NotificationService);
  private readonly i18n = inject(TranslationService);

  readonly isEdit = !!this.data.risk;
  readonly ratings = [1, 2, 3, 4, 5];
  readonly strategies = RISK_STRATEGIES;

  readonly ownerOptions = computed<SelectOption[]>(() =>
    this.userLookup.options().map((u) => ({ value: u.id, label: u.displayName })),
  );
  readonly entityOptions = computed<SelectOption[]>(() =>
    this.entityLookup.options().map((e) => ({ value: e.id, label: e.name })),
  );

  readonly form = this.fb.nonNullable.group({
    title: [this.data.risk?.title ?? '', [Validators.required, Validators.maxLength(300)]],
    description: [this.data.risk?.description ?? ''],
    category: [this.data.risk?.category ?? '', [Validators.required, Validators.maxLength(100)]],
    ownerUserId: [this.data.risk?.ownerUserId ?? null as string | null, Validators.required],
    auditableEntityId: [this.data.risk?.auditableEntityId ?? null as string | null],
    inherentLikelihood: [this.data.risk?.inherentLikelihood ?? 3, Validators.required],
    inherentImpact: [this.data.risk?.inherentImpact ?? 3, Validators.required],
    residualLikelihood: [this.data.risk?.residualLikelihood ?? null as number | null],
    residualImpact: [this.data.risk?.residualImpact ?? null as number | null],
    treatmentStrategy: [this.data.risk?.treatmentStrategy ?? null as RiskTreatmentStrategy | null],
    treatmentPlan: [this.data.risk?.treatmentPlan ?? ''],
    targetDate: [this.data.risk?.targetDate ?? ''],
    nextReviewDate: [this.data.risk?.nextReviewDate ?? ''],
  });

  private readonly value = toSignal(this.form.valueChanges, { initialValue: this.form.getRawValue() });

  /** Live inherent score + band chip. */
  readonly inherent = computed(() => {
    const v = this.value();
    const score = (v.inherentLikelihood ?? 0) * (v.inherentImpact ?? 0);
    return { score, band: bandOf(score) };
  });

  /** Live residual score + band chip (null until both set). */
  readonly residual = computed<{ score: number; band: RiskBand } | null>(() => {
    const v = this.value();
    if (v.residualLikelihood == null || v.residualImpact == null) {
      return null;
    }
    const score = v.residualLikelihood * v.residualImpact;
    return { score, band: bandOf(score) };
  });

  constructor() {
    this.userLookup.ensureLoaded();
    this.entityLookup.ensureLoaded();
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();

    if ((v.residualLikelihood == null) !== (v.residualImpact == null)) {
      this.notify.warning(this.i18n.translate('risk.warn.residualPair'));
      return;
    }
    if (this.residual() && this.residual()!.score > this.inherent().score) {
      this.notify.warning(this.i18n.translate('risk.warn.residualExceeds'));
      return;
    }

    const common = {
      title: v.title.trim(),
      description: v.description.trim() || null,
      category: v.category.trim(),
      ownerUserId: v.ownerUserId!,
      auditableEntityId: v.auditableEntityId || null,
      inherentLikelihood: Number(v.inherentLikelihood),
      inherentImpact: Number(v.inherentImpact),
      targetDate: v.targetDate || null,
    };

    if (this.isEdit && this.data.risk) {
      this.dialogRef.close({
        mode: 'update',
        id: this.data.risk.id,
        body: {
          ...common,
          residualLikelihood: v.residualLikelihood ?? null,
          residualImpact: v.residualImpact ?? null,
          treatmentStrategy: v.treatmentStrategy || null,
          treatmentPlan: v.treatmentPlan.trim() || null,
          nextReviewDate: v.nextReviewDate || null,
          version: this.data.risk.version,
        },
      });
    } else {
      this.dialogRef.close({ mode: 'register', body: common });
    }
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
