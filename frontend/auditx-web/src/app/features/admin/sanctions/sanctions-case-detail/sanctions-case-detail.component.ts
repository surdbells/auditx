import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  input,
  signal,
} from '@angular/core';
import { HttpErrorResponse, HttpResponse } from '@angular/common/http';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { IconComponent } from '../../../../core/icons/icon.component';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RouterLink } from '@angular/router';
import { Observable } from 'rxjs';

import { SanctionsService } from '../../../../core/services/sanctions.service';
import { UserLookupService } from '../../../../core/services/user-lookup.service';
import { ReferenceDataLookupService } from '../../../../core/services/reference-data-lookup.service';
import { NotificationService } from '../../../../core/services/notification.service';
import { AuthService } from '../../../../core/services/auth.service';
import { Permissions } from '../../../../core/permissions';
import { ProblemDetails, SanctionsCase } from '../../../../core/models';
import { humanise } from '../humanise';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../../shared/components/page-header/page-header.component';
import { PageGuideComponent } from '../../../../shared/components/page-guide/page-guide.component';
import { PageGuide } from '../../../../core/models/page-guide.models';
import {
  RecommendationDialogComponent,
  RecommendationDialogData,
  RecommendationDialogResult,
} from '../dialogs/recommendation-dialog.component';
import {
  HrOutcomeDialogComponent,
  HrOutcomeDialogResult,
} from '../dialogs/hr-outcome-dialog.component';
import {
  DcDecisionDialogComponent,
  DcDecisionDialogResult,
} from '../dialogs/dc-decision-dialog.component';
import {
  DecideAppealDialogComponent,
  DecideAppealDialogResult,
} from '../dialogs/decide-appeal-dialog.component';
import {
  SanctionsReasonDialogComponent,
  SanctionsReasonDialogData,
  SanctionsReasonResult,
} from '../dialogs/sanctions-reason-dialog.component';

type ViewState = 'loading' | 'ready' | 'error';

const MASKED_SUBJECT = 'EMPLOYEE_REDACTED';
const CONCURRENCY_CONFLICT = 'sanctions.concurrency_conflict';

/** Contextual page guide for a single sanctions case (walkthrough + "About this page" panel). */
const SANCTIONS_CASE_GUIDE: PageGuide = {
  id: 'sanctions-case-detail',
  titleKey: 'sanctions.caseDetail.guide.pageTitle',
  purposeKey: 'sanctions.caseDetail.guide.purpose',
  descriptionKey: 'sanctions.caseDetail.guide.description',
  actionKeys: [
    'sanctions.caseDetail.guide.action.recommend',
    'sanctions.caseDetail.guide.action.outcome',
    'sanctions.caseDetail.guide.action.appeal',
    'sanctions.caseDetail.guide.action.dossier',
  ],
  sections: [
    { selector: '.detail__status-row', titleKey: 'sanctions.caseDetail.guide.section.status.title', bodyKey: 'sanctions.caseDetail.guide.section.status.body' },
    { selector: '.detail__card', titleKey: 'sanctions.caseDetail.guide.section.overview.title', bodyKey: 'sanctions.caseDetail.guide.section.overview.body' },
    { selector: '.detail__actions', titleKey: 'sanctions.caseDetail.guide.section.dossier.title', bodyKey: 'sanctions.caseDetail.guide.section.dossier.body' },
  ],
  workflowKeys: [
    'sanctions.caseDetail.guide.flow.trigger',
    'sanctions.caseDetail.guide.flow.recommend',
    'sanctions.caseDetail.guide.flow.decision',
    'sanctions.caseDetail.guide.flow.appeal',
    'sanctions.caseDetail.guide.flow.close',
  ],
  dependsOnKeys: [
    'sanctions.caseDetail.guide.dep.exception',
    'sanctions.caseDetail.guide.dep.grid',
    'sanctions.caseDetail.guide.dep.directory',
  ],
  usedByKeys: [
    'sanctions.caseDetail.guide.use.dossier',
    'sanctions.caseDetail.guide.use.analytics',
    'sanctions.caseDetail.guide.use.audit',
  ],
  businessRuleKeys: [
    'sanctions.caseDetail.guide.rule.lifecycle',
    'sanctions.caseDetail.guide.rule.deviation',
    'sanctions.caseDetail.guide.rule.masking',
    'sanctions.caseDetail.guide.rule.appeal',
  ],
  tipKeys: [
    'sanctions.caseDetail.guide.tip.grid',
    'sanctions.caseDetail.guide.tip.dossier',
  ],
  permissionKeys: [
    'sanctions.caseDetail.guide.perm.recommend',
    'sanctions.caseDetail.guide.perm.decide',
    'sanctions.caseDetail.guide.perm.view',
  ],
  faq: [
    { questionKey: 'sanctions.caseDetail.guide.faq.masked.q', answerKey: 'sanctions.caseDetail.guide.faq.masked.a' },
    { questionKey: 'sanctions.caseDetail.guide.faq.appeal.q', answerKey: 'sanctions.caseDetail.guide.faq.appeal.a' },
  ],
};

@Component({
  selector: 'app-sanctions-case-detail',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    RouterLink,
    MatCardModule,
    MatButtonModule,
    IconComponent,
    MatTooltipModule,
    TranslatePipe,
    LoadingComponent,
    ErrorStateComponent,
    PageHeaderComponent,
    PageGuideComponent,
  ],
  templateUrl: './sanctions-case-detail.component.html',
  styleUrl: './sanctions-case-detail.component.scss',
})
export class SanctionsCaseDetailComponent {
  /** Route param bound via withComponentInputBinding. */
  readonly id = input.required<string>();

  private readonly service = inject(SanctionsService);
  /** Resolves the unmasked subject user id to a display name. */
  private readonly userLookup = inject(UserLookupService);
  private readonly refLookup = inject(ReferenceDataLookupService);
  private readonly notify = inject(NotificationService);
  private readonly auth = inject(AuthService);
  private readonly dialog = inject(MatDialog);
  private readonly i18n = inject(TranslationService);

  readonly state = signal<ViewState>('loading');
  readonly sanctionsCase = signal<SanctionsCase | null>(null);

  readonly humanise = humanise;
  readonly maskedSubject = MASKED_SUBJECT;
  readonly guide = SANCTIONS_CASE_GUIDE;

  readonly status = computed(() => this.sanctionsCase()?.status ?? null);

  readonly subjectLabel = computed(() => {
    const c = this.sanctionsCase();
    if (!c || c.subjectMasked || !c.subjectUserId) {
      return MASKED_SUBJECT;
    }
    return this.userLookup.displayName(c.subjectUserId);
  });

  categoryLabel(code: string | null | undefined): string {
    return code ? this.refLookup.label('sanction_category', code) : '—';
  }

  /* ---- Permissions ---- */

  readonly canRecommend = computed(() =>
    this.auth.hasPermission(Permissions.RecommendSanction),
  );
  readonly canRecordHrOutcome = computed(() =>
    this.auth.hasPermission(Permissions.RecordHrOutcome),
  );
  readonly canReferToDc = computed(() =>
    this.auth.hasPermission(Permissions.ReferToDc),
  );
  readonly canDcDecide = computed(() =>
    this.auth.hasPermission(Permissions.DcMember),
  );
  readonly canFileAppeal = computed(() =>
    this.auth.hasPermission(Permissions.FileAppeal),
  );
  readonly canDecideAppeal = computed(() =>
    this.auth.hasPermission(Permissions.DecideAppeal),
  );
  readonly canViewDossier = computed(() =>
    this.auth.hasPermission(Permissions.ViewSanctions),
  );

  /* ---- Status-driven action gating (mirrors the backend state machine) ---- */

  readonly canEditRecommendation = computed(
    () => this.canRecommend() && this.status() === 'recommendation_drafted',
  );
  readonly canSubmit = computed(
    () =>
      this.canRecommend() &&
      this.status() === 'recommendation_drafted' &&
      !!this.sanctionsCase()?.recommendation,
  );
  readonly canRecordHrOutcomeNow = computed(
    () =>
      this.canRecordHrOutcome() &&
      this.status() === 'recommendation_submitted',
  );
  readonly canReferToDcNow = computed(
    () => this.canReferToDc() && this.status() === 'recommendation_submitted',
  );
  readonly canDcDecideNow = computed(
    () => this.canDcDecide() && this.status() === 'dc_referral',
  );
  /** Appeals may be filed from either decision state. */
  readonly canFileAppealNow = computed(
    () =>
      this.canFileAppeal() &&
      (this.status() === 'hr_outcome_recorded' ||
        this.status() === 'dc_decision_recorded'),
  );
  /** The routed authority decides the appeal while the case is `appealed`. */
  readonly canDecideAppealNow = computed(
    () => this.canDecideAppeal() && this.status() === 'appealed',
  );
  /** Closure is legal from any terminal-eligible decision state. */
  readonly canCloseNow = computed(
    () =>
      this.canViewDossier() &&
      (this.status() === 'hr_outcome_recorded' ||
        this.status() === 'dc_decision_recorded' ||
        this.status() === 'appeal_decision_recorded'),
  );
  readonly canGenerateDossier = computed(
    () => this.canViewDossier() && this.status() !== null,
  );

  constructor() {
    queueMicrotask(() => this.fetch());
  }

  fetch(): void {
    this.state.set('loading');
    this.service.getById(this.id()).subscribe({
      next: (c) => {
        this.sanctionsCase.set(c);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  reload(): void {
    this.service.getById(this.id()).subscribe({
      next: (c) => this.sanctionsCase.set(c),
    });
  }

  private version(): string {
    return this.sanctionsCase()!.version;
  }

  /** Applies a fresh case returned by a mutation and toasts. */
  private runMutation(op: Observable<SanctionsCase>, message: string): void {
    op.subscribe({
      next: (c) => {
        this.sanctionsCase.set(c);
        this.notify.success(message);
      },
      error: (err: unknown) => this.handleError(err),
    });
  }

  private handleError(err: unknown): void {
    if (err instanceof HttpErrorResponse && err.status === 409) {
      const problem = err.error as ProblemDetails | null;
      if (problem?.error_code === CONCURRENCY_CONFLICT) {
        // Interceptor already toasted; reload for a fresh version.
        this.reload();
      }
    }
  }

  /* ---- Recommendation ---- */

  recordRecommendation(): void {
    const c = this.sanctionsCase();
    if (!c) {
      return;
    }
    const data: RecommendationDialogData = {
      recommendation: c.recommendation,
      gridRecommendedRange: c.gridRecommendedRange,
      gridConsultedVersion: c.gridConsultedVersion,
      deviationReason: c.deviationReason,
    };
    this.dialog
      .open(RecommendationDialogComponent, { data, width: '560px' })
      .afterClosed()
      .subscribe((result?: RecommendationDialogResult) => {
        if (!result) {
          return;
        }
        this.runMutation(
          this.service.recordRecommendation(this.id(), {
            recommendation: result.recommendation,
            deviationReason: result.deviationReason,
            version: this.version(),
          }),
          this.i18n.translate('sanctions.notify.recommendationRecorded'),
        );
      });
  }

  submit(): void {
    this.runMutation(
      this.service.submit(this.id(), { version: this.version() }),
      this.i18n.translate('sanctions.notify.recommendationSubmitted'),
    );
  }

  /* ---- HR outcome / DC referral / DC decision ---- */

  recordHrOutcome(): void {
    this.dialog
      .open(HrOutcomeDialogComponent, { width: '480px' })
      .afterClosed()
      .subscribe((result?: HrOutcomeDialogResult) => {
        if (!result) {
          return;
        }
        this.runMutation(
          this.service.recordHrOutcome(this.id(), {
            outcomeType: result.outcomeType,
            detail: result.detail,
            version: this.version(),
          }),
          this.i18n.translate('sanctions.notify.hrOutcomeRecorded'),
        );
      });
  }

  referToDc(): void {
    const data: SanctionsReasonDialogData = {
      title: this.i18n.translate('sanctions.referDc.title'),
      message: this.i18n.translate('sanctions.referDc.message'),
      label: this.i18n.translate('sanctions.referDc.label'),
      minLength: 20,
      confirmLabel: this.i18n.translate('sanctions.referDc.confirm'),
    };
    this.openReason(data, (reason) =>
      this.runMutation(
        this.service.referToDc(this.id(), {
          referralReason: reason,
          version: this.version(),
        }),
        this.i18n.translate('sanctions.notify.referred'),
      ),
    );
  }

  recordDcDecision(): void {
    this.dialog
      .open(DcDecisionDialogComponent, { width: '480px' })
      .afterClosed()
      .subscribe((result?: DcDecisionDialogResult) => {
        if (!result) {
          return;
        }
        this.runMutation(
          this.service.recordDcDecision(this.id(), {
            decision: result.decision,
            rationale: result.rationale,
            votingRecord: result.votingRecord,
            version: this.version(),
          }),
          this.i18n.translate('sanctions.notify.dcDecisionRecorded'),
        );
      });
  }

  /* ---- Appeals ---- */

  fileAppeal(): void {
    const data: SanctionsReasonDialogData = {
      title: this.i18n.translate('sanctions.action.fileAppeal'),
      message: this.i18n.translate('sanctions.fileAppeal.message'),
      label: this.i18n.translate('sanctions.fileAppeal.label'),
      confirmLabel: this.i18n.translate('sanctions.action.fileAppeal'),
    };
    this.openReason(data, (reason) => {
      this.service
        .fileAppeal(this.id(), { basis: reason, version: this.version() })
        .subscribe({
          next: () => {
            this.notify.success(this.i18n.translate('sanctions.notify.appealFiled'));
            this.reload();
          },
          error: (err: unknown) => this.handleError(err),
        });
    });
  }

  decideAppeal(): void {
    // The decision is keyed by the appeal id + the appeal's own concurrency version, both surfaced on the
    // case detail (latestAppeal*). The action is only shown while the case is in the appealed state.
    const appealId = this.sanctionsCase()?.latestAppealId ?? null;
    const appealVersion = this.sanctionsCase()?.latestAppealVersion ?? null;
    if (!appealId || !appealVersion) {
      return;
    }
    this.dialog
      .open(DecideAppealDialogComponent, { width: '480px' })
      .afterClosed()
      .subscribe((result?: DecideAppealDialogResult) => {
        if (!result) {
          return;
        }
        this.service
          .decideAppeal(appealId, {
            outcome: result.outcome,
            rationale: result.rationale,
            version: appealVersion,
          })
          .subscribe({
            next: () => {
              this.notify.success(this.i18n.translate('sanctions.notify.appealDecisionRecorded'));
              this.reload();
            },
            error: (err: unknown) => this.handleError(err),
          });
      });
  }

  /* ---- Close ---- */

  close(): void {
    this.runMutation(
      this.service.close(this.id(), { version: this.version() }),
      this.i18n.translate('sanctions.notify.caseClosed'),
    );
  }

  /* ---- Dossier (raw HTML file + X-Dossier-Sha256 header) ---- */

  generateDossier(): void {
    this.service.generateDossier(this.id()).subscribe({
      next: (res) => this.handleDossier(res),
      error: (err: unknown) => this.handleError(err),
    });
  }

  private handleDossier(res: HttpResponse<Blob>): void {
    const blob = res.body;
    if (!blob) {
      return;
    }
    const sha = res.headers.get('X-Dossier-Sha256');
    const filename =
      this.filenameFromDisposition(
        res.headers.get('Content-Disposition'),
      ) ?? `sanctions-dossier-${this.id()}.html`;
    this.triggerDownload(blob, filename);
    this.notify.success(
      sha
        ? this.i18n.translate('sanctions.notify.dossierDownloadedSha', { sha })
        : this.i18n.translate('sanctions.notify.dossierDownloaded'),
    );
  }

  private filenameFromDisposition(disposition: string | null): string | null {
    if (!disposition) {
      return null;
    }
    const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition);
    return match ? decodeURIComponent(match[1]) : null;
  }

  private triggerDownload(blob: Blob, filename: string): void {
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
  }

  private openReason(
    config: SanctionsReasonDialogData,
    onResult: (reason: string) => void,
  ): void {
    this.dialog
      .open(SanctionsReasonDialogComponent, { data: config, width: '480px' })
      .afterClosed()
      .subscribe((result?: SanctionsReasonResult) => {
        if (!result) {
          return;
        }
        onResult(result.reason);
      });
  }
}
