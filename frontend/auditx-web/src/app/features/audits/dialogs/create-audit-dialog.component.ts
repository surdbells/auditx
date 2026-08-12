import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import {
  MAT_DIALOG_DATA,
  MatDialogModule,
  MatDialogRef,
} from '@angular/material/dialog';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { provideNativeDateAdapter } from '@angular/material/core';
import { MatFormFieldModule } from '@angular/material/form-field';
import { IconComponent } from '../../../core/icons/icon.component';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';

import { ReferenceDataLookupService } from '../../../core/services/reference-data-lookup.service';
import { CreateAuditRequest, TemplateListItem, UserDto } from '../../../core/models';

export interface CreateAuditDialogData {
  /** Users selectable as lead / auditee / team members. */
  users: UserDto[];
  /** Published templates; selecting one preloads its checklist into the new audit. */
  templates: TemplateListItem[];
  /**
   * When launching an audit from an approved annual-plan item, its context — the form is prefilled from it and
   * the created audit links back to the plan item (which the plan then marks completed on audit completion).
   */
  planItem?: CreateAuditPlanItemContext;
}

/** The annual-plan item an audit is being launched from. */
export interface CreateAuditPlanItemContext {
  planItemId: string;
  auditType: string;
  /** Human label for the audit type (banner display). */
  auditTypeLabel: string;
  leadUserId: string | null;
  /** Which of the plan item's (possibly several) entities this audit is for. */
  entityId: string;
  /** Human label for the audited entity (for the suggested name + banner). */
  entityName: string;
  /** Pre-suggested audit name, e.g. "Branch Ops — 2026 Plan". */
  suggestedName: string;
  /** Planned window (yyyy-MM-dd) copied into the audit's start / target dates. */
  plannedStartDate?: string;
  plannedEndDate?: string;
}

/** Converts a Date to an ISO `yyyy-MM-dd` DateOnly string. */
function toDateOnly(value: Date | null): string {
  if (!value) {
    return '';
  }
  const y = value.getFullYear();
  const m = String(value.getMonth() + 1).padStart(2, '0');
  const d = String(value.getDate()).padStart(2, '0');
  return `${y}-${m}-${d}`;
}

/** Parses an ISO `yyyy-MM-dd` DateOnly string into a local Date (inverse of {@link toDateOnly}). */
function fromDateOnly(value: string | undefined): Date | null {
  if (!value) {
    return null;
  }
  const [y, m, d] = value.split('-').map(Number);
  if (!y || !m || !d) {
    return null;
  }
  return new Date(y, m - 1, d);
}

@Component({
  selector: 'app-create-audit-dialog',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [provideNativeDateAdapter()],
  imports: [
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatDatepickerModule,
    MatButtonModule,
    IconComponent,
  ],
  template: `
    <h2 mat-dialog-title>{{ data.planItem ? 'Launch audit' : 'New audit' }}</h2>
    <mat-dialog-content>
      @if (data.planItem; as pi) {
        <div class="plan-banner">
          <app-icon name="event_available" />
          <span>Launching from the annual plan — <strong>{{ pi.entityName }}</strong> ({{ pi.auditTypeLabel }}). The audit will be linked to this plan item.</span>
        </div>
      }
      <form [formGroup]="form" class="form">
        <mat-form-field appearance="outline" class="full">
          <mat-label>Name</mat-label>
          <input matInput formControlName="name" autocomplete="off" />
          @if (form.controls.name.hasError('required') && form.controls.name.touched) {
            <mat-error>A name is required.</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline" class="full">
          <mat-label>Audit type</mat-label>
          <mat-select formControlName="auditType">
            @for (o of auditTypes(); track o.code) {
              <mat-option [value]="o.code">{{ o.label }}</mat-option>
            }
          </mat-select>
          @if (form.controls.auditType.hasError('required') && form.controls.auditType.touched) {
            <mat-error>An audit type is required.</mat-error>
          }
        </mat-form-field>

        <mat-form-field appearance="outline" class="full">
          <mat-label>Template</mat-label>
          <mat-select
            formControlName="templateId"
            (selectionChange)="onTemplateSelected($event.value)"
          >
            <mat-option [value]="''">None (blank checklist)</mat-option>
            @for (t of data.templates; track t.id) {
              <mat-option [value]="t.id">{{ t.name }} ({{ t.auditType }})</mat-option>
            }
          </mat-select>
          <mat-hint>
            Optional. Preloads the template's checklist items into the audit — you can still add or edit items afterward.
          </mat-hint>
        </mat-form-field>

        <mat-form-field appearance="outline" class="full">
          <mat-label>Scope description</mat-label>
          <textarea matInput formControlName="scopeDescription" rows="2"></textarea>
        </mat-form-field>

        <div class="row">
          <mat-form-field appearance="outline">
            <mat-label>Start date</mat-label>
            <input matInput [matDatepicker]="startPicker" formControlName="startDate" />
            <mat-datepicker-toggle matIconSuffix [for]="startPicker" />
            <mat-datepicker #startPicker />
            @if (form.controls.startDate.hasError('required') && form.controls.startDate.touched) {
              <mat-error>A start date is required.</mat-error>
            }
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Target end date</mat-label>
            <input matInput [matDatepicker]="endPicker" formControlName="targetEndDate" />
            <mat-datepicker-toggle matIconSuffix [for]="endPicker" />
            <mat-datepicker #endPicker />
          </mat-form-field>
        </div>

        <div class="row">
          <mat-form-field appearance="outline">
            <mat-label>Lead</mat-label>
            <mat-select formControlName="leadUserId">
              @for (u of data.users; track u.id) {
                <mat-option [value]="u.id">{{ u.displayName }}</mat-option>
              }
            </mat-select>
            @if (form.controls.leadUserId.hasError('required') && form.controls.leadUserId.touched) {
              <mat-error>Select a lead.</mat-error>
            }
          </mat-form-field>
          <mat-form-field appearance="outline">
            <mat-label>Auditee</mat-label>
            <mat-select formControlName="auditeeUserId">
              @for (u of data.users; track u.id) {
                <mat-option [value]="u.id">{{ u.displayName }}</mat-option>
              }
            </mat-select>
            @if (form.controls.auditeeUserId.hasError('required') && form.controls.auditeeUserId.touched) {
              <mat-error>Select an auditee.</mat-error>
            }
          </mat-form-field>
        </div>

        <mat-form-field appearance="outline" class="full">
          <mat-label>Team members</mat-label>
          <mat-select formControlName="teamMemberUserIds" multiple>
            @for (u of teamCandidates(); track u.id) {
              <mat-option [value]="u.id">{{ u.displayName }}</mat-option>
            }
          </mat-select>
          <mat-hint>Optional auditors added to the team.</mat-hint>
        </mat-form-field>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button matButton type="button" (click)="cancel()">Cancel</button>
      <button matButton="filled" type="button" (click)="submit()" [disabled]="form.invalid">
        Create
      </button>
    </mat-dialog-actions>
  `,
  styles: `
    .plan-banner {
      display: flex;
      align-items: flex-start;
      gap: 0.6rem;
      padding: 0.75rem 1rem;
      margin-bottom: 1rem;
      border-radius: var(--ax-radius, 12px);
      background: var(--mat-sys-secondary-container);
      color: var(--mat-sys-on-secondary-container);
      font: var(--mat-sys-body-small);
    }
    .plan-banner mat-icon {
      flex: 0 0 auto;
    }
    .form {
      display: flex;
      flex-direction: column;
      min-width: 460px;
    }
    .row {
      display: flex;
      gap: 1rem;
    }
    .row mat-form-field {
      flex: 1;
    }
    .full {
      width: 100%;
    }
    @media (max-width: 560px) {
      .form {
        min-width: auto;
      }
      .row {
        flex-direction: column;
      }
    }
  `,
})
export class CreateAuditDialogComponent {
  readonly data = inject<CreateAuditDialogData>(MAT_DIALOG_DATA);
  private readonly dialogRef =
    inject<MatDialogRef<CreateAuditDialogComponent, CreateAuditRequest>>(
      MatDialogRef,
    );
  private readonly fb = inject(FormBuilder);
  private readonly refLookup = inject(ReferenceDataLookupService);

  /** Active audit-type reference-data items (lazy-loaded). */
  readonly auditTypes = this.refLookup.options('audit_type');

  readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(200)]],
    auditType: ['', [Validators.required]],
    templateId: [''],
    scopeDescription: [''],
    startDate: [null as Date | null, [Validators.required]],
    targetEndDate: [null as Date | null],
    leadUserId: ['', [Validators.required]],
    auditeeUserId: ['', [Validators.required]],
    teamMemberUserIds: [[] as string[]],
  });

  /** Exclude the chosen lead / auditee from the team-member list. */
  readonly teamCandidates = computed(() => this.data.users);

  constructor() {
    // Launching from an annual-plan item: prefill name, type, lead and the planned window.
    const pi = this.data.planItem;
    if (pi) {
      this.form.patchValue({
        name: pi.suggestedName,
        auditType: pi.auditType,
        leadUserId: pi.leadUserId ?? '',
        startDate: fromDateOnly(pi.plannedStartDate),
        targetEndDate: fromDateOnly(pi.plannedEndDate),
      });
    }
  }

  /** Selecting a template aligns the audit type to that template's type. */
  onTemplateSelected(templateId: string): void {
    const template = this.data.templates.find((t) => t.id === templateId);
    if (template) {
      this.form.controls.auditType.setValue(template.auditType);
    }
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    const team = v.teamMemberUserIds.filter(
      (id) => id !== v.leadUserId && id !== v.auditeeUserId,
    );
    this.dialogRef.close({
      name: v.name.trim(),
      auditType: v.auditType.trim(),
      templateId: v.templateId || null,
      planItemId: this.data.planItem?.planItemId ?? null,
      entityId: this.data.planItem?.entityId ?? null,
      scopeDescription: v.scopeDescription.trim() || null,
      startDate: toDateOnly(v.startDate),
      targetEndDate: v.targetEndDate ? toDateOnly(v.targetEndDate) : null,
      leadUserId: v.leadUserId,
      auditeeUserId: v.auditeeUserId,
      teamMemberUserIds: team.length ? team : undefined,
      backdatingOverride: false,
    });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
