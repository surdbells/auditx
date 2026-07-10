import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatChipsModule, MatChipInputEvent } from '@angular/material/chips';
import {
  MAT_DIALOG_DATA,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';

import {
  REPORT_CADENCES,
  ReportCadence,
  ReportSchedule,
  StandaloneReportKind,
  UserDto,
} from '../../../core/models';
import { AuthService } from '../../../core/services/auth.service';
import { Permissions } from '../../../core/permissions';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';

export interface ReportScheduleDialogData {
  /** Present = edit mode (kind is fixed); absent = create. */
  schedule?: ReportSchedule;
  /** Active users for the recipient picker. */
  users: UserDto[];
}

export interface ReportScheduleFormResult {
  name: string;
  kind: StandaloneReportKind;
  cadence: ReportCadence;
  recipientUserIds: string[];
  recipientEmails: string[];
  isActive: boolean;
}

interface KindOption {
  value: StandaloneReportKind;
  labelKey: string;
}

/** Standalone report kinds a schedule may target (mirrors the on-demand standalone generator). */
const KIND_OPTIONS: KindOption[] = [
  { value: 'executive_summary', labelKey: 'reports.standalone.kind.executiveSummary' },
  { value: 'annual_plan_status', labelKey: 'reports.standalone.kind.annualPlanStatus' },
  { value: 'kpi_pack', labelKey: 'reports.standalone.kind.kpiPack' },
  { value: 'audit_coverage', labelKey: 'reports.standalone.kind.auditCoverage' },
  { value: 'findings_register', labelKey: 'reports.standalone.kind.findingsRegister' },
  { value: 'sanctions_consistency', labelKey: 'reports.standalone.kind.sanctionsConsistency' },
  { value: 'performance_scorecards', labelKey: 'reports.standalone.kind.performanceScorecards' },
];

/** Kinds whose content (and thus scheduling) needs an extra permission beyond ScheduleReports. */
const RESTRICTED_KINDS: Record<string, string> = {
  performance_scorecards: Permissions.PerformanceAnalyticsView,
};

const EMAIL_RE = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

/** Create / edit a recurring report schedule (D3-C). Recipients = directory users (picker) + ad-hoc emails (chips). */
@Component({
  selector: 'app-report-schedule-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatChipsModule,
    MatSlideToggleModule,
    MatButtonModule,
    MatIconModule,
    TranslatePipe,
  ],
  template: `
    <h2 mat-dialog-title>
      {{ (isEdit ? 'reportSchedules.dialog.editTitle' : 'reportSchedules.dialog.createTitle') | t }}
    </h2>
    <mat-dialog-content>
      <form [formGroup]="form" class="form">
        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ 'reportSchedules.field.name' | t }}</mat-label>
          <input matInput formControlName="name" autocomplete="off" maxlength="120" />
          @if (form.controls.name.hasError('required') && form.controls.name.touched) {
            <mat-error>{{ 'reportSchedules.error.nameRequired' | t }}</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ 'reportSchedules.field.kind' | t }}</mat-label>
          <mat-select formControlName="kind">
            @for (k of kindOptions; track k.value) {
              <mat-option [value]="k.value">{{ k.labelKey | t }}</mat-option>
            }
          </mat-select>
          @if (isEdit) {
            <mat-hint>{{ 'reportSchedules.field.kindFixed' | t }}</mat-hint>
          }
        </mat-form-field>

        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ 'reportSchedules.field.cadence' | t }}</mat-label>
          <mat-select formControlName="cadence">
            @for (c of cadences; track c) {
              <mat-option [value]="c">{{ 'reportSchedules.cadence.' + c | t }}</mat-option>
            }
          </mat-select>
        </mat-form-field>

        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ 'reportSchedules.field.recipients' | t }}</mat-label>
          <mat-select formControlName="userIds" multiple>
            @for (u of data.users; track u.id) {
              <mat-option [value]="u.id">{{ u.displayName }} ({{ u.email }})</mat-option>
            }
          </mat-select>
          <mat-hint>{{ 'reportSchedules.field.recipientsHint' | t }}</mat-hint>
        </mat-form-field>

        <mat-form-field appearance="outline" class="full">
          <mat-label>{{ 'reportSchedules.field.emails' | t }}</mat-label>
          <mat-chip-grid #chipGrid [attr.aria-label]="'reportSchedules.field.emails' | t">
            @for (email of emails(); track email) {
              <mat-chip-row (removed)="removeEmail(email)">
                {{ email }}
                <button matChipRemove [attr.aria-label]="'reportSchedules.field.removeEmail' | t: { email: email }">
                  <mat-icon>cancel</mat-icon>
                </button>
              </mat-chip-row>
            }
            <input
              [placeholder]="'reportSchedules.field.emailPlaceholder' | t"
              [matChipInputFor]="chipGrid"
              (matChipInputTokenEnd)="addEmail($event)"
            />
          </mat-chip-grid>
          @if (emailError()) {
            <mat-error>{{ 'reportSchedules.error.emailInvalid' | t }}</mat-error>
          } @else {
            <mat-hint>{{ 'reportSchedules.field.emailsHint' | t }}</mat-hint>
          }
        </mat-form-field>

        @if (isEdit) {
          <mat-slide-toggle formControlName="isActive">
            {{ 'reportSchedules.field.active' | t }}
          </mat-slide-toggle>
        }
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="cancel()">{{ 'reports.common.cancel' | t }}</button>
      <button matButton="filled" type="button" (click)="submit()" [disabled]="!canSubmit()">
        {{ 'reports.common.save' | t }}
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .form {
      display: flex;
      flex-direction: column;
      gap: 4px;
      min-width: 460px;
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
export class ReportScheduleDialogComponent {
  readonly data = inject<ReportScheduleDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef =
    inject<MatDialogRef<ReportScheduleDialogComponent, ReportScheduleFormResult>>(MatDialogRef);
  private readonly fb = inject(FormBuilder);
  private readonly auth = inject(AuthService);

  readonly cadences = REPORT_CADENCES;
  readonly isEdit = !!this.data.schedule;
  // Hide kinds the user can't schedule (e.g. scorecards without PerformanceAnalyticsView) — but keep the current
  // kind visible in edit mode, where it is fixed and the select must still render it.
  readonly kindOptions = this.visibleKinds();

  readonly emails = signal<string[]>(this.data.schedule?.recipientEmails ?? []);
  readonly emailError = signal(false);

  readonly form = this.fb.nonNullable.group({
    name: [this.data.schedule?.name ?? '', [Validators.required, Validators.maxLength(120)]],
    kind: [{ value: this.data.schedule?.kind ?? ('executive_summary' as StandaloneReportKind), disabled: this.isEdit }],
    cadence: [this.data.schedule?.cadence ?? ('monthly' as ReportCadence)],
    userIds: [this.data.schedule?.recipientUserIds ?? ([] as string[])],
    isActive: [this.data.schedule?.isActive ?? true],
  });

  canSubmit(): boolean {
    const v = this.form.getRawValue();
    return this.form.valid && (v.userIds.length > 0 || this.emails().length > 0);
  }

  addEmail(event: MatChipInputEvent): void {
    const value = (event.value ?? '').trim();
    if (!value) {
      event.chipInput?.clear();
      return;
    }
    if (!EMAIL_RE.test(value)) {
      this.emailError.set(true);
      return;
    }
    this.emailError.set(false);
    if (!this.emails().includes(value)) {
      this.emails.update((list) => [...list, value]);
    }
    event.chipInput?.clear();
  }

  removeEmail(email: string): void {
    this.emails.update((list) => list.filter((e) => e !== email));
  }

  submit(): void {
    if (!this.canSubmit()) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    this.dialogRef.close({
      name: v.name.trim(),
      kind: v.kind,
      cadence: v.cadence,
      recipientUserIds: v.userIds,
      recipientEmails: this.emails(),
      isActive: v.isActive,
    });
  }

  cancel(): void {
    this.dialogRef.close();
  }

  private canScheduleKind(kind: string): boolean {
    const required = RESTRICTED_KINDS[kind];
    return !required || this.auth.hasPermission(required);
  }

  private visibleKinds(): KindOption[] {
    const allowed = KIND_OPTIONS.filter((o) => this.canScheduleKind(o.value));
    const current = this.data.schedule?.kind;
    if (current && !allowed.some((o) => o.value === current)) {
      const opt = KIND_OPTIONS.find((o) => o.value === current);
      if (opt) {
        allowed.push(opt);
      }
    }
    return allowed;
  }
}
