import {
  ChangeDetectionStrategy,
  Component,
  computed,
  inject,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTableModule } from '@angular/material/table';

import { AdministrationService } from '../../../../core/services/administration.service';
import { NotificationService } from '../../../../core/services/notification.service';
import { AuthService } from '../../../../core/services/auth.service';
import { Permissions } from '../../../../core/permissions';
import {
  ObjectRestoreRequest,
  RestoreDrill,
  RestoreDrillOutcome,
} from '../../../../core/models';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { PageGuideComponent } from '../../../../shared/components/page-guide/page-guide.component';
import { PageGuide } from '../../../../core/models/page-guide.models';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';

type ViewState = 'loading' | 'ready' | 'error';

/** Contextual page guide for the backup & restore tab (walkthrough + About panel). */
const BACKUP_GUIDE: PageGuide = {
  id: 'administration-backup-restore',
  titleKey: 'administration.tabs.backup',
  purposeKey: 'administration.backup.guide.purpose',
  descriptionKey: 'administration.backup.guide.description',
  actionKeys: [
    'administration.backup.guide.action.drill',
    'administration.backup.guide.action.request',
    'administration.backup.guide.action.decide',
    'administration.backup.guide.action.history',
  ],
  sections: [
    { selector: '[data-guide="drill"]', titleKey: 'administration.backup.guide.section.drill.title', bodyKey: 'administration.backup.guide.section.drill.body' },
    { selector: '[data-guide="restore"]', titleKey: 'administration.backup.guide.section.restore.title', bodyKey: 'administration.backup.guide.section.restore.body' },
    { selector: '.restore__history-card', titleKey: 'administration.backup.guide.section.history.title', bodyKey: 'administration.backup.guide.section.history.body' },
  ],
  workflowKeys: [
    'administration.backup.guide.flow.backup',
    'administration.backup.guide.flow.drill',
    'administration.backup.guide.flow.record',
    'administration.backup.guide.flow.request',
    'administration.backup.guide.flow.approve',
  ],
  dependsOnKeys: [
    'administration.backup.guide.dep.backups',
    'administration.backup.guide.dep.retention',
    'administration.backup.guide.dep.rbac',
  ],
  usedByKeys: [
    'administration.backup.guide.use.compliance',
    'administration.backup.guide.use.auditTrail',
    'administration.backup.guide.use.resilience',
  ],
  businessRuleKeys: [
    'administration.backup.guide.rule.dualControl',
    'administration.backup.guide.rule.justification',
    'administration.backup.guide.rule.outcome',
    'administration.backup.guide.rule.immutable',
  ],
  tipKeys: [
    'administration.backup.guide.tip.cadence',
    'administration.backup.guide.tip.details',
    'administration.backup.guide.tip.snapshot',
  ],
  permissionKeys: [
    'administration.backup.guide.perm.retention',
    'administration.backup.guide.perm.restore',
    'administration.backup.guide.perm.separation',
  ],
  faq: [
    { questionKey: 'administration.backup.guide.faq.drill.q', answerKey: 'administration.backup.guide.faq.drill.a' },
    { questionKey: 'administration.backup.guide.faq.undo.q', answerKey: 'administration.backup.guide.faq.undo.a' },
  ],
};

@Component({
  selector: 'app-backup-restore',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    ReactiveFormsModule,
    MatCardModule,
    MatTableModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    LoadingComponent,
    ErrorStateComponent,
    PageGuideComponent,
    TranslatePipe,
  ],
  templateUrl: './backup-restore.component.html',
  styleUrl: './backup-restore.component.scss',
})
export class BackupRestoreComponent {
  private readonly admin = inject(AdministrationService);
  private readonly notify = inject(NotificationService);
  private readonly auth = inject(AuthService);
  private readonly fb = inject(FormBuilder);
  private readonly i18n = inject(TranslationService);

  readonly guide = BACKUP_GUIDE;

  readonly drillColumns = ['executedAt', 'outcome', 'details'];

  readonly outcomes: RestoreDrillOutcome[] = ['Success', 'Failed'];

  readonly state = signal<ViewState>('loading');
  readonly drills = signal<RestoreDrill[]>([]);
  readonly recording = signal(false);
  readonly requesting = signal(false);
  readonly deciding = signal(false);
  readonly lastRequest = signal<ObjectRestoreRequest | null>(null);

  readonly canManageRetention = computed(() =>
    this.auth.hasPermission(Permissions.ManageRetention),
  );
  readonly canExecRestore = computed(() =>
    this.auth.hasPermission(Permissions.ExecRestore),
  );

  readonly drillForm = this.fb.nonNullable.group({
    outcome: ['Success' as RestoreDrillOutcome, Validators.required],
    details: ['', Validators.required],
  });

  readonly restoreForm = this.fb.nonNullable.group({
    objectType: ['', Validators.required],
    objectId: ['', Validators.required],
    snapshotDate: ['', Validators.required],
    justification: ['', Validators.required],
  });

  readonly decisionForm = this.fb.nonNullable.group({
    comment: [''],
  });

  constructor() {
    this.fetch();
  }

  fetch(): void {
    this.state.set('loading');
    this.admin.listRestoreDrills().subscribe({
      next: (items) => {
        this.drills.set(items);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  recordDrill(): void {
    if (this.drillForm.invalid) {
      this.drillForm.markAllAsTouched();
      return;
    }
    const v = this.drillForm.getRawValue();
    this.recording.set(true);
    this.admin
      .createRestoreDrill({ outcome: v.outcome, details: v.details.trim() })
      .subscribe({
        next: () => {
          this.notify.success(
            this.i18n.translate('administration.backup.notify.drillRecorded'),
          );
          this.drillForm.reset({ outcome: 'Success', details: '' });
          this.recording.set(false);
          this.fetch();
        },
        error: () => this.recording.set(false),
      });
  }

  requestRestore(): void {
    if (this.restoreForm.invalid) {
      this.restoreForm.markAllAsTouched();
      return;
    }
    const v = this.restoreForm.getRawValue();
    this.requesting.set(true);
    this.admin
      .requestObjectRestore({
        objectType: v.objectType.trim(),
        objectId: v.objectId.trim(),
        snapshotDate: v.snapshotDate,
        justification: v.justification.trim(),
      })
      .subscribe({
        next: (req) => {
          this.lastRequest.set(req);
          this.notify.success(
            this.i18n.translate('administration.backup.notify.restoreRequested'),
          );
          this.restoreForm.reset();
          this.requesting.set(false);
        },
        error: () => this.requesting.set(false),
      });
  }

  decide(approve: boolean): void {
    const req = this.lastRequest();
    if (!req) {
      return;
    }
    this.deciding.set(true);
    this.admin
      .decideObjectRestore(req.id, {
        approve,
        comment: this.decisionForm.controls.comment.value.trim(),
      })
      .subscribe({
        next: (updated) => {
          this.lastRequest.set(updated);
          this.notify.success(
            this.i18n.translate(
              approve
                ? 'administration.backup.notify.restoreApproved'
                : 'administration.backup.notify.restoreRejected',
            ),
          );
          this.deciding.set(false);
        },
        error: () => this.deciding.set(false),
      });
  }
}
