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
import { PaginatorComponent } from '../../../../shared/components/paginator/paginator.component';
import { PageGuideComponent } from '../../../../shared/components/page-guide/page-guide.component';
import { PageGuide } from '../../../../core/models/page-guide.models';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';

type ViewState = 'loading' | 'ready' | 'error';

const DEFAULT_PAGE_SIZE = 25;

/** Contextual page guide for the configuration domain detail (walkthrough + About panel). */
const CONFIGURATION_DETAIL_GUIDE: PageGuide = {
  id: 'configuration-detail',
  titleKey: 'configuration.detail.guide.pageTitle',
  purposeKey: 'configuration.detail.guide.purpose',
  descriptionKey: 'configuration.detail.guide.description',
  actionKeys: [
    'configuration.detail.guide.action.review',
    'configuration.detail.guide.action.draft',
    'configuration.detail.guide.action.activate',
    'configuration.detail.guide.action.rollback',
  ],
  sections: [
    { selector: '.config__defs', titleKey: 'configuration.detail.guide.section.active.title', bodyKey: 'configuration.detail.guide.section.active.body' },
    { selector: '.config__form', titleKey: 'configuration.detail.guide.section.draft.title', bodyKey: 'configuration.detail.guide.section.draft.body' },
    { selector: '.config__table-card', titleKey: 'configuration.detail.guide.section.history.title', bodyKey: 'configuration.detail.guide.section.history.body' },
  ],
  workflowKeys: [
    'configuration.detail.guide.flow.draft',
    'configuration.detail.guide.flow.review',
    'configuration.detail.guide.flow.activate',
    'configuration.detail.guide.flow.apply',
  ],
  dependsOnKeys: [
    'configuration.detail.guide.dep.domains',
    'configuration.detail.guide.dep.approvals',
    'configuration.detail.guide.dep.permission',
  ],
  usedByKeys: [
    'configuration.detail.guide.use.exceptions',
    'configuration.detail.guide.use.analytics',
    'configuration.detail.guide.use.reports',
  ],
  businessRuleKeys: [
    'configuration.detail.guide.rule.single',
    'configuration.detail.guide.rule.reason',
    'configuration.detail.guide.rule.checker',
    'configuration.detail.guide.rule.immutable',
  ],
  tipKeys: [
    'configuration.detail.guide.tip.reason',
    'configuration.detail.guide.tip.rollback',
    'configuration.detail.guide.tip.refresh',
  ],
  permissionKeys: [
    'configuration.detail.guide.perm.manage',
    'configuration.detail.guide.perm.viewer',
  ],
  faq: [
    { questionKey: 'configuration.detail.guide.faq.pending.q', answerKey: 'configuration.detail.guide.faq.pending.a' },
    { questionKey: 'configuration.detail.guide.faq.rollback.q', answerKey: 'configuration.detail.guide.faq.rollback.a' },
  ],
};

/** Translation key for the title shown for each known domain. */
const DOMAIN_TITLE_KEYS: Record<string, string> = {
  [CONFIG_DOMAIN_EXCEPTION_DEFAULTS]: 'config.detail.exceptionDefaults.title',
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
    PaginatorComponent,
    PageGuideComponent,
    TranslatePipe,
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
  private readonly i18n = inject(TranslationService);

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
  readonly total = signal(0);
  readonly page = signal(1);
  readonly pageSize = signal(DEFAULT_PAGE_SIZE);
  /** In-flight version-history fetch (page navigation) — disables the paginator without clearing the table. */
  readonly loading = signal(false);

  readonly saving = signal(false);
  /** Banner text when an action was routed to a second approver. */
  readonly pendingBanner = signal<string | null>(null);
  /** Inline 422 field validation messages from the last draft save. */
  readonly fieldErrors = signal<FieldError[]>([]);

  readonly minReason = MIN_REASON;

  readonly guide = CONFIGURATION_DETAIL_GUIDE;

  readonly title = computed(() => {
    const key = DOMAIN_TITLE_KEYS[this.domain()];
    return key ? this.i18n.translate(key) : this.domain();
  });

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
        this.fetchPage(1);
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

  fetchPage(page: number): void {
    this.loading.set(true);
    this.service.listVersions(this.domain(), page, this.pageSize()).subscribe({
      next: (result) => {
        this.versions.set(result.items);
        this.total.set(result.total);
        this.page.set(result.page);
        this.loading.set(false);
      },
      error: () => this.loading.set(false),
    });
  }

  onPageChange(page: number): void {
    this.fetchPage(page);
  }

  onPageSizeChange(size: number): void {
    this.pageSize.set(size);
    this.fetchPage(1);
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
          this.notify.success(
            this.i18n.translate('config.detail.toast.draftCreated', {
              version: draft.versionNumber,
            }),
          );
          this.form.controls.changeReason.reset('');
          this.fetchPage(1);
        },
        error: (err: unknown) => {
          this.saving.set(false);
          this.captureFieldErrors(err);
        },
      });
  }

  activate(v: ConfigurationVersion): void {
    const data: ConfigReasonDialogData = {
      title: this.i18n.translate('config.detail.activate.dialogTitle', {
        version: v.versionNumber,
      }),
      message: this.i18n.translate('config.detail.activate.dialogMessage', {
        version: v.versionNumber,
        title: this.title(),
        count: MIN_REASON,
      }),
      confirmLabel: this.i18n.translate('config.detail.activate.confirmLabel'),
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
                  this.i18n.translate('config.detail.activate.pendingBanner', {
                    version: v.versionNumber,
                    actionId: action.pendingActionId,
                  }),
                );
                this.notify.info(
                  this.i18n.translate('config.detail.activate.submittedInfo'),
                );
              } else {
                this.notify.success(
                  this.i18n.translate(
                    'config.detail.activate.activatedSuccess',
                    { version: v.versionNumber },
                  ),
                );
                if (action.version) {
                  this.applyActive(action.version);
                }
                this.fetchPage(this.page());
              }
            },
          });
      });
  }

  rollback(v: ConfigurationVersion): void {
    const data: ConfigReasonDialogData = {
      title: this.i18n.translate('config.detail.rollback.dialogTitle', {
        version: v.versionNumber,
      }),
      message: this.i18n.translate('config.detail.rollback.dialogMessage', {
        version: v.versionNumber,
        count: MIN_REASON,
      }),
      confirmLabel: this.i18n.translate('config.detail.rollback.confirmLabel'),
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
                  this.i18n.translate('config.detail.rollback.pendingBanner', {
                    version: v.versionNumber,
                    actionId: action.pendingActionId,
                  }),
                );
                this.notify.info(
                  this.i18n.translate('config.detail.rollback.submittedInfo'),
                );
              } else {
                this.notify.success(
                  this.i18n.translate(
                    'config.detail.rollback.rolledBackSuccess',
                    { version: v.versionNumber },
                  ),
                );
                if (action.version) {
                  this.applyActive(action.version);
                }
                this.fetchPage(this.page());
              }
            },
            error: (err: unknown) => {
              if (err instanceof HttpErrorResponse && err.status === 409) {
                this.notify.warning(
                  this.i18n.translate(
                    'config.detail.rollback.alreadyActiveWarning',
                  ),
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
