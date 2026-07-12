import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';

import { TimeTrackingService } from '../../../core/services/time-tracking.service';
import { NotificationService } from '../../../core/services/notification.service';
import { AuthService } from '../../../core/services/auth.service';
import { UserLookupService } from '../../../core/services/user-lookup.service';
import { Permissions } from '../../../core/permissions';
import {
  Audit,
  TIME_CATEGORIES,
  TimeCategory,
  TimeEntry,
  TimeSummary,
} from '../../../core/models';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';
import { BarChartComponent } from '../../../shared/charts/bar-chart/bar-chart.component';
import { ChartDatum } from '../../../shared/charts/chart-types';

/** Statuses in which time may be logged (mirrors the backend Loggable set). */
const LOGGABLE = new Set(['planned', 'in_progress', 'under_review', 'completed']);

/**
 * Time & effort panel for the audit-execution screen (P0-B). Self-manages its own
 * time entries + budget summary (time entries do NOT change the audit version), and
 * emits {@link budgetChanged} after a budget edit so the parent reloads the audit.
 */
@Component({
  selector: 'app-audit-time-panel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    TranslatePipe,
    BarChartComponent,
  ],
  templateUrl: './audit-time-panel.component.html',
  styleUrl: './audit-time-panel.component.scss',
})
export class AuditTimePanelComponent {
  readonly audit = input.required<Audit>();
  /**
   * When set, the panel is scoped to a single checklist item: the list filters to that item,
   * new entries are pre-attached to it, and the item picker is hidden. Null = whole-audit view.
   */
  readonly scopedItemId = input<string | null>(null);
  /** Emitted after a budget change (bumps the audit version) so the parent reloads. */
  readonly budgetChanged = output<void>();

  readonly isScoped = computed(() => !!this.scopedItemId());

  /** Entries shown in the list — filtered to the scoped item when scoped, else all. */
  readonly visibleEntries = computed(() => {
    const scope = this.scopedItemId();
    return scope ? this.entries().filter((e) => e.checklistItemId === scope) : this.entries();
  });

  private readonly service = inject(TimeTrackingService);
  private readonly notify = inject(NotificationService);
  private readonly auth = inject(AuthService);
  private readonly fb = inject(FormBuilder);
  private readonly i18n = inject(TranslationService);
  /** Resolves contributor ids to names (lazy global directory). */
  private readonly lookup = inject(UserLookupService);
  private readonly destroyRef = inject(DestroyRef);

  readonly categories = TIME_CATEGORIES;

  readonly entries = signal<TimeEntry[]>([]);
  readonly summary = signal<TimeSummary | null>(null);
  readonly loading = signal(true);
  readonly submitting = signal(false);
  readonly savingBudget = signal(false);

  /** The entry being edited (null = the form is in "log new" mode). */
  readonly editing = signal<TimeEntry | null>(null);
  private lastAuditId = '';

  readonly me = computed(() => this.auth.session()?.userId ?? null);

  readonly canLog = computed(() => this.auth.hasPermission(Permissions.LogTime));
  readonly canManageBudget = computed(() => this.auth.hasPermission(Permissions.ManageAudit));
  readonly canLogNow = computed(() => this.canLog() && LOGGABLE.has(this.audit().status));

  readonly form = this.fb.nonNullable.group({
    workDate: ['', Validators.required],
    hours: [null as number | null, [Validators.required, Validators.min(0.1), Validators.max(24)]],
    category: ['fieldwork' as TimeCategory, Validators.required],
    checklistItemId: ['' as string],
    notes: [''],
  });

  readonly budgetForm = this.fb.nonNullable.group({
    budgetedHours: [null as number | null, [Validators.min(0)]],
  });

  /** Percent of budget consumed, formatted; '—' when no budget is set. */
  readonly consumedLabel = computed(() => {
    const p = this.summary()?.percentConsumed;
    return p === null || p === undefined ? '—' : `${Math.round(p)}%`;
  });

  /** True when logged effort has exceeded the budget (variance negative) — the only "bad" state. */
  readonly overBudget = computed(() => {
    const v = this.summary()?.varianceHours;
    return v !== null && v !== undefined && v < 0;
  });

  readonly categoryChart = computed<ChartDatum[]>(() =>
    (this.summary()?.byCategory ?? []).map((c) => ({
      label: this.categoryLabel(c.category),
      value: c.hours,
    })),
  );

  constructor() {
    effect(() => {
      const id = this.audit().id;
      if (id && id !== this.lastAuditId) {
        this.lastAuditId = id;
        this.budgetForm.controls.budgetedHours.setValue(this.audit().budgetedHours);
        this.refresh();
      }
    });
    // Keep the (hidden) related-item control pinned to the scoped item so new entries attach to it.
    effect(() => {
      const scope = this.scopedItemId();
      if (scope && !this.editing()) {
        this.form.controls.checklistItemId.setValue(scope);
      }
    });
    this.destroyRef.onDestroy(() => this.clearTimer());
  }

  refresh(): void {
    const id = this.audit().id;
    this.loading.set(true);
    this.service.list(id).subscribe({
      next: (rows) => {
        this.entries.set(rows);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
    this.service.summary(id).subscribe({ next: (s) => this.summary.set(s) });
  }

  nameOf(userId: string | null | undefined): string {
    return this.lookup.displayName(userId);
  }

  categoryLabel(category: string): string {
    return this.i18n.translate(`time.category.${category}`);
  }

  promptFor(checklistItemId: string | null): string | null {
    if (!checklistItemId) {
      return null;
    }
    return this.audit().checklistItems.find((i) => i.id === checklistItemId)?.prompt ?? null;
  }

  /** Only the entry's owner or an audit manager may edit/delete it. */
  canModify(entry: TimeEntry): boolean {
    return entry.userId === this.me() || this.canManageBudget();
  }

  /* ---- Quick capture: one-tap chips + an opt-in stopwatch ---- */

  /** Preset durations (hours) offered as one-tap "+15m / +30m / +1h" chips. */
  readonly quickHours = [0.25, 0.5, 1];

  /** Local (not UTC) yyyy-mm-dd for "today", so the smart default matches the auditor's calendar day. */
  private today(): string {
    const d = new Date();
    const p = (n: number) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${p(d.getMonth() + 1)}-${p(d.getDate())}`;
  }

  /** Human label for a preset chip (0.25 → "15m", 1 → "1h"). */
  quickLabel(hours: number): string {
    return hours < 1 ? `${Math.round(hours * 60)}m` : `${hours}h`;
  }

  /** One-tap log: today, category Fieldwork, attached to the scoped item (or none). */
  logQuick(hours: number): void {
    if (this.submitting()) {
      return;
    }
    this.submitting.set(true);
    this.service
      .log(this.audit().id, {
        workDate: this.today(),
        hours,
        category: 'fieldwork',
        checklistItemId: this.scopedItemId() || null,
        notes: null,
      })
      .subscribe({
        next: () => {
          this.submitting.set(false);
          this.notify.success(this.i18n.translate('time.notify.logged'));
          this.refresh();
        },
        error: (err: unknown) => {
          this.submitting.set(false);
          this.onError(err);
        },
      });
  }

  readonly timerRunning = signal(false);
  readonly elapsedSec = signal(0);
  private timerHandle: ReturnType<typeof setInterval> | null = null;
  private timerStartMs = 0;

  /** Elapsed stopwatch as HH:MM:SS. */
  readonly elapsedLabel = computed(() => {
    const s = this.elapsedSec();
    const p = (n: number) => String(n).padStart(2, '0');
    return `${p(Math.floor(s / 3600))}:${p(Math.floor((s % 3600) / 60))}:${p(s % 60)}`;
  });

  startTimer(): void {
    if (this.timerRunning()) {
      return;
    }
    this.timerStartMs = Date.now() - this.elapsedSec() * 1000;
    this.timerRunning.set(true);
    this.timerHandle = setInterval(
      () => this.elapsedSec.set(Math.floor((Date.now() - this.timerStartMs) / 1000)),
      1000,
    );
  }

  /**
   * Stops the stopwatch and drops the elapsed time into the log form (rounded up to the nearest
   * 15 min, min 15) so the auditor can review + adjust before logging. Deliberately never auto-logs.
   */
  stopTimer(): void {
    this.clearTimer();
    this.timerRunning.set(false);
    const hours = Math.max(0.25, Math.ceil((this.elapsedSec() / 3600) * 4) / 4);
    this.editing.set(null);
    this.form.patchValue({
      workDate: this.today(),
      hours,
      category: 'fieldwork',
      checklistItemId: this.scopedItemId() ?? '',
    });
  }

  resetTimer(): void {
    this.clearTimer();
    this.timerRunning.set(false);
    this.elapsedSec.set(0);
  }

  private clearTimer(): void {
    if (this.timerHandle) {
      clearInterval(this.timerHandle);
      this.timerHandle = null;
    }
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    const body = {
      workDate: v.workDate,
      hours: Number(v.hours),
      category: v.category,
      checklistItemId: v.checklistItemId || null,
      notes: v.notes.trim() || null,
    };
    this.submitting.set(true);

    const editing = this.editing();
    const op = editing
      ? this.service.update(editing.id, { ...body, version: editing.version })
      : this.service.log(this.audit().id, body);

    op.subscribe({
      next: () => {
        this.submitting.set(false);
        this.notify.success(
          this.i18n.translate(editing ? 'time.notify.updated' : 'time.notify.logged'),
        );
        this.cancelEdit();
        this.refresh();
      },
      error: (err: unknown) => {
        this.submitting.set(false);
        this.onError(err);
      },
    });
  }

  edit(entry: TimeEntry): void {
    this.editing.set(entry);
    this.form.setValue({
      workDate: entry.workDate,
      hours: entry.hours,
      category: entry.category,
      checklistItemId: entry.checklistItemId ?? '',
      notes: entry.notes ?? '',
    });
  }

  cancelEdit(): void {
    this.editing.set(null);
    this.form.reset({
      category: 'fieldwork',
      checklistItemId: this.scopedItemId() ?? '',
      notes: '',
      workDate: '',
      hours: null,
    });
  }

  remove(entry: TimeEntry): void {
    this.service.delete(entry.id, entry.version).subscribe({
      next: () => {
        this.notify.success(this.i18n.translate('time.notify.deleted'));
        if (this.editing()?.id === entry.id) {
          this.cancelEdit();
        }
        this.refresh();
      },
      error: (err: unknown) => this.onError(err),
    });
  }

  saveBudget(): void {
    if (this.budgetForm.invalid) {
      return;
    }
    const hours = this.budgetForm.controls.budgetedHours.value;
    this.savingBudget.set(true);
    this.service.setBudget(this.audit().id, { budgetedHours: hours ?? null, version: this.audit().version }).subscribe({
      next: () => {
        this.savingBudget.set(false);
        this.notify.success(this.i18n.translate('time.notify.budgetSaved'));
        this.budgetChanged.emit(); // parent reloads the audit (version bumped)
        this.refresh();
      },
      error: (err: unknown) => {
        this.savingBudget.set(false);
        this.onError(err);
      },
    });
  }

  private onError(err: unknown): void {
    if (err instanceof HttpErrorResponse && err.status === 409) {
      this.notify.error(this.i18n.translate('time.error.conflict'));
      this.refresh();
    }
    // Other errors already surfaced by the global interceptor.
  }
}
