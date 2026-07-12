import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  signal,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { IconComponent } from '../../../core/icons/icon.component';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';

import { ProceduresService } from '../../../core/services/procedures.service';
import { NotificationService } from '../../../core/services/notification.service';
import { AuthService } from '../../../core/services/auth.service';
import { UserLookupService } from '../../../core/services/user-lookup.service';
import { Permissions } from '../../../core/permissions';
import {
  Audit,
  AuditProcedure,
  PROCEDURE_TYPES,
  ProcedureType,
  SAMPLING_METHODS,
  SamplingMethod,
} from '../../../core/models';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';

/** Audit statuses in which a procedure may be recorded (mirrors the backend Recordable set). */
const RECORDABLE = new Set(['in_progress', 'under_review']);

/**
 * Typed execution-procedures panel for the audit-execution screen (P2-C). Self-manages its own
 * procedures list; procedures are a separate aggregate so they do NOT change the audit version.
 */
@Component({
  selector: 'app-audit-procedures-panel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    IconComponent,
    TranslatePipe,
  ],
  templateUrl: './audit-procedures-panel.component.html',
  styleUrl: './audit-procedures-panel.component.scss',
})
export class AuditProceduresPanelComponent {
  readonly audit = input.required<Audit>();
  /** When set, the panel is scoped to one checklist item (filter + pre-attach + hide the item picker). */
  readonly scopedItemId = input<string | null>(null);

  readonly isScoped = computed(() => !!this.scopedItemId());

  /** Procedures shown — filtered to the scoped item when scoped, else all. */
  readonly visibleProcedures = computed(() => {
    const scope = this.scopedItemId();
    return scope ? this.procedures().filter((p) => p.checklistItemId === scope) : this.procedures();
  });

  private readonly service = inject(ProceduresService);
  private readonly notify = inject(NotificationService);
  private readonly auth = inject(AuthService);
  private readonly fb = inject(FormBuilder);
  private readonly i18n = inject(TranslationService);
  private readonly lookup = inject(UserLookupService);

  readonly types = PROCEDURE_TYPES;
  readonly methods = SAMPLING_METHODS;

  readonly procedures = signal<AuditProcedure[]>([]);
  readonly loading = signal(true);
  readonly submitting = signal(false);
  readonly adding = signal(false);
  private lastAuditId = '';

  private readonly me = computed(() => this.auth.session()?.userId ?? null);

  readonly canRecord = computed(() => this.auth.hasPermission(Permissions.RespondItem));
  readonly canManage = computed(() => this.auth.hasPermission(Permissions.ManageAudit));
  readonly canRecordNow = computed(() => this.canRecord() && RECORDABLE.has(this.audit().status));

  readonly form = this.fb.nonNullable.group({
    type: ['sampling' as ProcedureType, Validators.required],
    performedOn: ['', Validators.required],
    checklistItemId: [''],
    summary: ['', [Validators.required, Validators.maxLength(2000)]],
    counterparty: [''],
    population: [null as number | null, Validators.min(0)],
    sampleSize: [null as number | null, Validators.min(0)],
    itemsTested: [null as number | null, Validators.min(0)],
    exceptionsFound: [null as number | null, Validators.min(0)],
    method: ['' as SamplingMethod | ''],
  });

  readonly isSampling = computed(() => this.form.controls.type.value === 'sampling');

  constructor() {
    // The user directory loads lazily the first time a row resolves a name (mirrors the time panel).
    effect(() => {
      const id = this.audit().id;
      if (id && id !== this.lastAuditId) {
        this.lastAuditId = id;
        this.refresh();
      }
    });
  }

  refresh(): void {
    this.loading.set(true);
    this.service.list(this.audit().id).subscribe({
      next: (rows) => {
        this.procedures.set(rows);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  nameOf(userId: string | null | undefined): string {
    return this.lookup.displayName(userId);
  }

  promptFor(checklistItemId: string | null): string | null {
    if (!checklistItemId) {
      return null;
    }
    return this.audit().checklistItems.find((i) => i.id === checklistItemId)?.prompt ?? null;
  }

  /** Sample error rate for a sampling row (exceptions / tested), or null. */
  errorRate(p: AuditProcedure): number | null {
    if (p.itemsTested && p.itemsTested > 0 && p.exceptionsFound !== null) {
      return Math.round((p.exceptionsFound / p.itemsTested) * 1000) / 10;
    }
    return null;
  }

  canModify(p: AuditProcedure): boolean {
    return p.performedByUserId === this.me() || this.canManage();
  }

  startAdd(): void {
    this.adding.set(true);
    this.form.reset({
      type: 'sampling',
      performedOn: '',
      checklistItemId: this.scopedItemId() ?? '',
      summary: '',
      counterparty: '',
      population: null,
      sampleSize: null,
      itemsTested: null,
      exceptionsFound: null,
      method: '',
    });
  }

  cancelAdd(): void {
    this.adding.set(false);
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    const sampling = v.type === 'sampling';
    this.submitting.set(true);
    this.service
      .record(this.audit().id, {
        type: v.type,
        performedOn: v.performedOn,
        checklistItemId: v.checklistItemId || null,
        summary: v.summary.trim(),
        counterparty: sampling ? null : v.counterparty.trim() || null,
        population: sampling ? v.population : null,
        sampleSize: sampling ? v.sampleSize : null,
        itemsTested: sampling ? v.itemsTested : null,
        exceptionsFound: sampling ? v.exceptionsFound : null,
        method: sampling ? v.method || null : null,
      })
      .subscribe({
        next: () => {
          this.submitting.set(false);
          this.notify.success(this.i18n.translate('procedure.notify.recorded'));
          this.adding.set(false);
          this.refresh();
        },
        error: () => this.submitting.set(false),
      });
  }

  remove(p: AuditProcedure): void {
    this.service.delete(p.id, p.version).subscribe({
      next: () => {
        this.notify.success(this.i18n.translate('procedure.notify.deleted'));
        this.refresh();
      },
    });
  }
}
