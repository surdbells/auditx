import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatTableModule } from '@angular/material/table';
import { RouterLink } from '@angular/router';

import { ConfigurationService } from '../../../../core/services/configuration.service';
import { NotificationService } from '../../../../core/services/notification.service';
import { AuthService } from '../../../../core/services/auth.service';
import { Permissions } from '../../../../core/permissions';
import {
  CONFIG_DOMAIN_EXCEPTION_DEFAULTS,
  ConfigurationVersion,
  ExceptionDefaultsDefinition,
  FieldError,
  ProblemDetails,
} from '../../../../core/models';
import {
  ConfigReasonDialogComponent,
  ConfigReasonDialogData,
  ConfigReasonResult,
} from '../dialogs/config-reason-dialog.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';

type ViewState = 'loading' | 'ready' | 'error';

/** Title shown for each known domain. */
const DOMAIN_TITLES: Record<string, string> = {
  [CONFIG_DOMAIN_EXCEPTION_DEFAULTS]: 'Exception defaults & SLAs',
};

const MIN_REASON = 20;

/**
 * Detail view for a single configuration domain: shows the active version's
 * values, a typed draft editor, and a cursor-paged version history with
 * activate / rollback actions. Activate / rollback are maker-checker-gateable:
 * a 202 surfaces a "pending second approver" banner; a 200 refreshes the active
 * version in place.
 */
@Component({
  selector: 'app-configuration-detail',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    RouterLink,
    ReactiveFormsModule,
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatIconModule,
    LoadingComponent,
    ErrorStateComponent,
    PageHeaderComponent,
  ],
  templateUrl: './configuration-detail.component.html',
  styleUrl: './configuration-detail.component.scss',
})
export class ConfigurationDetailComponent {
  private readonly service = inject(ConfigurationService);
  private readonly notify = inject(NotificationService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly fb = inject(FormBuilder);

  /** Route param: the configuration domain (e.g. `exception_defaults`). */
  readonly domain = input.required<string>();

  readonly displayedColumns = [
    'versionNumber',
    'state',
    'changeReason',
    'created',
    'activated',
    'actions',
  ];

  readonly state = signal<ViewState>('loading');
  readonly active = signal<ConfigurationVersion | null>(null);
  readonly activeDefinition = signal<ExceptionDefaultsDefinition | null>(null);
  readonly versions = signal<ConfigurationVersion[]>([]);
  readonly nextCursor = signal<string | null>(null);
  readonly hasMore = signal(false);
  readonly loadingMore = signal(false);

  readonly saving = signal(false);
  /** Banner text when an action was routed to a second approver. */
  readonly pendingBanner = signal<string | null>(null);
  /** Inline 422 field validation messages from the last draft save. */
  readonly fieldErrors = signal<FieldError[]>([]);

  readonly minReason = MIN_REASON;

  readonly title = computed(
    () => DOMAIN_TITLES[this.domain()] ?? this.domain(),
  );

  readonly canManage = computed(() =>
    this.auth.hasPermission(Permissions.ManageConfiguration),
  );

  /** Typed draft editor form for the `exception_defaults` domain. */
  readonly form = this.fb.nonNullable.group({
    criticalTargetDays: [30, [Validators.required, Validators.min(1)]],
    highTargetDays: [60, [Validators.required, Validators.min(1)]],
    mediumTargetDays: [90, [Validators.required, Validators.min(1)]],
    lowTargetDays: [120, [Validators.required, Validators.min(1)]],
    recurrenceWindowMonths: [
      12,
      [Validators.required, Validators.min(1), Validators.max(120)],
    ],
    recurrenceThreshold: [2, [Validators.required, Validators.min(2)]],
    changeReason: [
      '',
      [Validators.required, Validators.minLength(MIN_REASON)],
    ],
  });

  constructor() {
    // Defer until route-bound inputs are set (mirrors the template editor).
    queueMicrotask(() => this.fetch());
  }

  fetch(): void {
    this.state.set('loading');
    this.pendingBanner.set(null);
    this.service.getActive(this.domain()).subscribe({
      next: (v) => {
        this.applyActive(v);
        this.loadVersions();
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  private applyActive(v: ConfigurationVersion): void {
    this.active.set(v);
    const def = this.service.parseExceptionDefaults(v.definitionJson);
    this.activeDefinition.set(def);
    if (def) {
      this.form.patchValue({
        criticalTargetDays: def.criticalTargetDays,
        highTargetDays: def.highTargetDays,
        mediumTargetDays: def.mediumTargetDays,
        lowTargetDays: def.lowTargetDays,
        recurrenceWindowMonths: def.recurrenceWindowMonths,
        recurrenceThreshold: def.recurrenceThreshold,
      });
    }
  }

  loadVersions(): void {
    this.service.listVersions(this.domain()).subscribe({
      next: (page) => {
        this.versions.set(page.items);
        this.nextCursor.set(page.nextCursor);
        this.hasMore.set(page.hasMore);
      },
    });
  }

  loadMore(): void {
    if (!this.hasMore() || this.loadingMore()) {
      return;
    }
    this.loadingMore.set(true);
    this.service.listVersions(this.domain(), this.nextCursor()).subscribe({
      next: (page) => {
        this.versions.update((rows) => [...rows, ...page.items]);
        this.nextCursor.set(page.nextCursor);
        this.hasMore.set(page.hasMore);
        this.loadingMore.set(false);
      },
      error: () => this.loadingMore.set(false),
    });
  }

  /** POSTs a new inactive draft from the typed form. */
  saveDraft(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    const definitionJson = this.service.serialiseExceptionDefaults({
      criticalTargetDays: v.criticalTargetDays,
      highTargetDays: v.highTargetDays,
      mediumTargetDays: v.mediumTargetDays,
      lowTargetDays: v.lowTargetDays,
      recurrenceWindowMonths: v.recurrenceWindowMonths,
      recurrenceThreshold: v.recurrenceThreshold,
    });

    this.saving.set(true);
    this.fieldErrors.set([]);
    this.service
      .createDraft(this.domain(), {
        definitionJson,
        changeReason: v.changeReason.trim(),
      })
      .subscribe({
        next: (draft) => {
          this.saving.set(false);
          this.notify.success(`Draft v${draft.versionNumber} created.`);
          this.form.controls.changeReason.reset('');
          this.loadVersions();
        },
        error: (err: unknown) => {
          this.saving.set(false);
          this.captureFieldErrors(err);
        },
      });
  }

  activate(v: ConfigurationVersion): void {
    const data: ConfigReasonDialogData = {
      title: `Activate v${v.versionNumber}`,
      message: `Make version ${v.versionNumber} the active configuration for "${this.title()}". Provide a change reason (at least ${MIN_REASON} characters).`,
      confirmLabel: 'Activate',
      minLength: MIN_REASON,
    };
    this.dialog
      .open(ConfigReasonDialogComponent, { data, width: '480px' })
      .afterClosed()
      .subscribe((result?: ConfigReasonResult) => {
        if (!result) {
          return;
        }
        this.pendingBanner.set(null);
        this.service
          .activateVersion(this.domain(), v.versionNumber, {
            changeReason: result.changeReason,
          })
          .subscribe({
            next: (action) => {
              if (action.pendingActionId) {
                this.pendingBanner.set(
                  `Activation of v${v.versionNumber} was submitted for a second approver (action ${action.pendingActionId}).`,
                );
                this.notify.info('Activation submitted for sign-off.');
              } else {
                this.notify.success(`Version ${v.versionNumber} activated.`);
                if (action.version) {
                  this.applyActive(action.version);
                }
                this.loadVersions();
              }
            },
          });
      });
  }

  rollback(v: ConfigurationVersion): void {
    const data: ConfigReasonDialogData = {
      title: `Roll back to v${v.versionNumber}`,
      message: `Roll the active configuration back to version ${v.versionNumber}. Provide a change reason (at least ${MIN_REASON} characters).`,
      confirmLabel: 'Roll back',
      minLength: MIN_REASON,
    };
    this.dialog
      .open(ConfigReasonDialogComponent, { data, width: '480px' })
      .afterClosed()
      .subscribe((result?: ConfigReasonResult) => {
        if (!result) {
          return;
        }
        this.pendingBanner.set(null);
        this.service
          .rollback(this.domain(), {
            toVersionNumber: v.versionNumber,
            changeReason: result.changeReason,
          })
          .subscribe({
            next: (action) => {
              if (action.pendingActionId) {
                this.pendingBanner.set(
                  `Rollback to v${v.versionNumber} was submitted for a second approver (action ${action.pendingActionId}).`,
                );
                this.notify.info('Rollback submitted for sign-off.');
              } else {
                this.notify.success(`Rolled back to v${v.versionNumber}.`);
                if (action.version) {
                  this.applyActive(action.version);
                }
                this.loadVersions();
              }
            },
            error: (err: unknown) => {
              if (err instanceof HttpErrorResponse && err.status === 409) {
                this.notify.warning(
                  'That version is already active — nothing to roll back.',
                );
              }
              // Other errors already surfaced by the global interceptor.
            },
          });
      });
  }

  private captureFieldErrors(err: unknown): void {
    if (err instanceof HttpErrorResponse && err.status === 422) {
      const problem = err.error as ProblemDetails | null;
      this.fieldErrors.set(problem?.field_errors ?? []);
    }
    // The global interceptor already toasts the message.
  }
}
