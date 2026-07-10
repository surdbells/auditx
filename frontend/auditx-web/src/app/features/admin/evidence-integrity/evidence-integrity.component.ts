import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';

import { AuditTrailService } from '../../../core/services/audit-trail.service';
import { NotificationService } from '../../../core/services/notification.service';
import { FlaggedEvidence } from '../../../core/models';
import {
  UnflagEvidenceDialogComponent,
  UnflagEvidenceDialogData,
  UnflagEvidenceDialogResult,
} from './dialogs/unflag-evidence-dialog.component';
import { LoadingComponent } from '../../../shared/components/loading/loading.component';
import { EmptyStateComponent } from '../../../shared/components/empty-state/empty-state.component';
import { ErrorStateComponent } from '../../../shared/components/error-state/error-state.component';
import { PageHeaderComponent } from '../../../shared/components/page-header/page-header.component';
import { PageGuideComponent } from '../../../shared/components/page-guide/page-guide.component';
import { PageGuide } from '../../../core/models/page-guide.models';
import { TranslatePipe } from '../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../core/i18n/translation.service';

type ViewState = 'loading' | 'ready' | 'error';

/** Contextual page guide for the evidence-integrity console (walkthrough + the About panel). */
const EVIDENCE_INTEGRITY_GUIDE: PageGuide = {
  id: 'evidence-integrity',
  titleKey: 'adminMisc.evidence.title',
  purposeKey: 'evidenceIntegrity.guide.purpose',
  descriptionKey: 'evidenceIntegrity.guide.description',
  actionKeys: [
    'evidenceIntegrity.guide.action.review',
    'evidenceIntegrity.guide.action.inspect',
    'evidenceIntegrity.guide.action.unflag',
    'evidenceIntegrity.guide.action.retry',
  ],
  sections: [
    {
      selector: '.integrity__table',
      titleKey: 'evidenceIntegrity.guide.section.table.title',
      bodyKey: 'evidenceIntegrity.guide.section.table.body',
    },
    {
      selector: '.integrity__hash',
      titleKey: 'evidenceIntegrity.guide.section.hash.title',
      bodyKey: 'evidenceIntegrity.guide.section.hash.body',
    },
    {
      selector: '[data-guide="unflag"]',
      titleKey: 'evidenceIntegrity.guide.section.unflag.title',
      bodyKey: 'evidenceIntegrity.guide.section.unflag.body',
    },
  ],
  workflowKeys: [
    'evidenceIntegrity.guide.flow.upload',
    'evidenceIntegrity.guide.flow.hash',
    'evidenceIntegrity.guide.flow.reverify',
    'evidenceIntegrity.guide.flow.flag',
    'evidenceIntegrity.guide.flow.resolve',
  ],
  dependsOnKeys: [
    'evidenceIntegrity.guide.dep.audits',
    'evidenceIntegrity.guide.dep.evidence',
    'evidenceIntegrity.guide.dep.trail',
  ],
  usedByKeys: [
    'evidenceIntegrity.guide.use.findings',
    'evidenceIntegrity.guide.use.reports',
    'evidenceIntegrity.guide.use.trail',
  ],
  businessRuleKeys: [
    'evidenceIntegrity.guide.rule.hashMatch',
    'evidenceIntegrity.guide.rule.immutable',
    'evidenceIntegrity.guide.rule.resolution',
    'evidenceIntegrity.guide.rule.tenant',
  ],
  tipKeys: [
    'evidenceIntegrity.guide.tip.empty',
    'evidenceIntegrity.guide.tip.reupload',
  ],
  permissionKeys: [
    'evidenceIntegrity.guide.perm.admin',
    'evidenceIntegrity.guide.perm.auditor',
  ],
  faq: [
    {
      questionKey: 'evidenceIntegrity.guide.faq.why.q',
      answerKey: 'evidenceIntegrity.guide.faq.why.a',
    },
    {
      questionKey: 'evidenceIntegrity.guide.faq.unflag.q',
      answerKey: 'evidenceIntegrity.guide.faq.unflag.a',
    },
  ],
};

@Component({
  selector: 'app-evidence-integrity',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    MatCardModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    LoadingComponent,
    EmptyStateComponent,
    ErrorStateComponent,
    PageHeaderComponent,
    PageGuideComponent,
    TranslatePipe,
  ],
  templateUrl: './evidence-integrity.component.html',
  styleUrl: './evidence-integrity.component.scss',
})
export class EvidenceIntegrityComponent {
  private readonly service = inject(AuditTrailService);
  private readonly notify = inject(NotificationService);
  private readonly dialog = inject(MatDialog);
  private readonly i18n = inject(TranslationService);

  readonly guide = EVIDENCE_INTEGRITY_GUIDE;

  readonly displayedColumns = [
    'filename',
    'auditId',
    'sha256',
    'uploadedAt',
    'actions',
  ];

  readonly state = signal<ViewState>('loading');
  readonly flagged = signal<FlaggedEvidence[]>([]);

  readonly isEmpty = computed(
    () => this.state() === 'ready' && this.flagged().length === 0,
  );

  constructor() {
    this.fetch();
  }

  fetch(): void {
    this.state.set('loading');
    this.service.listFlagged().subscribe({
      next: (items) => {
        this.flagged.set(items);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  unflag(evidence: FlaggedEvidence): void {
    const data: UnflagEvidenceDialogData = { evidence };
    this.dialog
      .open(UnflagEvidenceDialogComponent, { data, width: '520px' })
      .afterClosed()
      .subscribe((result?: UnflagEvidenceDialogResult) => {
        if (!result) {
          return;
        }
        this.service
          .unflagEvidence(evidence.auditId, evidence.id, result.resolution)
          .subscribe({
            next: () => {
              this.notify.success(
                this.i18n.translate('adminMisc.evidence.unflagged'),
              );
              this.fetch();
            },
          });
      });
  }
}
