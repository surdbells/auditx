import {
  ChangeDetectionStrategy,
  Component,
  inject,
  signal,
} from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule, MatChipInputEvent } from '@angular/material/chips';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { IconComponent } from '../../../../core/icons/icon.component';
import { MatInputModule } from '@angular/material/input';

import { AdministrationService } from '../../../../core/services/administration.service';
import { NotificationService } from '../../../../core/services/notification.service';
import { SupportChannelStatus } from '../../../../core/models';
import { TranslatePipe } from '../../../../core/i18n/translate.pipe';
import { TranslationService } from '../../../../core/i18n/translation.service';
import {
  ConfirmDialogComponent,
  ConfirmDialogData,
} from '../../../../shared/components/confirm-dialog/confirm-dialog.component';
import { LoadingComponent } from '../../../../shared/components/loading/loading.component';
import { ErrorStateComponent } from '../../../../shared/components/error-state/error-state.component';
import { PageGuideComponent } from '../../../../shared/components/page-guide/page-guide.component';
import { PageGuide } from '../../../../core/models/page-guide.models';

type ViewState = 'loading' | 'ready' | 'error';

/** Contextual page guide for the vendor support-channel (break-glass access) tab. */
const SUPPORT_CHANNEL_GUIDE: PageGuide = {
  id: 'administration-support-channel',
  titleKey: 'administration.support.guide.pageTitle',
  purposeKey: 'administration.support.guide.purpose',
  descriptionKey: 'administration.support.guide.description',
  actionKeys: [
    'administration.support.guide.action.status',
    'administration.support.guide.action.grant',
    'administration.support.guide.action.duration',
    'administration.support.guide.action.revoke',
  ],
  sections: [
    { selector: '[data-guide="status"]', titleKey: 'administration.support.guide.section.status.title', bodyKey: 'administration.support.guide.section.status.body' },
    { selector: '[data-guide="engineers"]', titleKey: 'administration.support.guide.section.engineers.title', bodyKey: 'administration.support.guide.section.engineers.body' },
    { selector: '[data-guide="grant"]', titleKey: 'administration.support.guide.section.grant.title', bodyKey: 'administration.support.guide.section.grant.body' },
  ],
  workflowKeys: [
    'administration.support.guide.flow.request',
    'administration.support.guide.flow.grant',
    'administration.support.guide.flow.assist',
    'administration.support.guide.flow.expire',
  ],
  dependsOnKeys: [
    'administration.support.guide.dep.permission',
    'administration.support.guide.dep.identity',
  ],
  usedByKeys: [
    'administration.support.guide.use.audit',
    'administration.support.guide.use.health',
  ],
  businessRuleKeys: [
    'administration.support.guide.rule.duration',
    'administration.support.guide.rule.expiry',
    'administration.support.guide.rule.revoke',
    'administration.support.guide.rule.identifiers',
  ],
  tipKeys: [
    'administration.support.guide.tip.shortest',
    'administration.support.guide.tip.revoke',
  ],
  permissionKeys: [
    'administration.support.guide.perm.manage',
  ],
  faq: [
    { questionKey: 'administration.support.guide.faq.expire.q', answerKey: 'administration.support.guide.faq.expire.a' },
    { questionKey: 'administration.support.guide.faq.who.q', answerKey: 'administration.support.guide.faq.who.a' },
  ],
};

@Component({
  selector: 'app-support-channel',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [
    DatePipe,
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatChipsModule,
    MatButtonModule,
    IconComponent,
    TranslatePipe,
    LoadingComponent,
    ErrorStateComponent,
    PageGuideComponent,
  ],
  templateUrl: './support-channel.component.html',
  styleUrl: './support-channel.component.scss',
})
export class SupportChannelComponent {
  private readonly admin = inject(AdministrationService);
  private readonly notify = inject(NotificationService);
  private readonly dialog = inject(MatDialog);
  private readonly fb = inject(FormBuilder);
  private readonly i18n = inject(TranslationService);

  readonly guide = SUPPORT_CHANNEL_GUIDE;

  readonly state = signal<ViewState>('loading');
  readonly status = signal<SupportChannelStatus | null>(null);
  readonly engineers = signal<string[]>([]);
  readonly submitting = signal(false);

  readonly form = this.fb.nonNullable.group({
    durationMinutes: [
      60,
      [Validators.required, Validators.min(1), Validators.max(1440)],
    ],
  });

  constructor() {
    this.fetch();
  }

  fetch(): void {
    this.state.set('loading');
    this.admin.getSupportChannel().subscribe({
      next: (s) => {
        this.status.set(s);
        this.state.set('ready');
      },
      error: () => this.state.set('error'),
    });
  }

  addEngineer(event: MatChipInputEvent): void {
    const value = (event.value ?? '').trim();
    if (value && !this.engineers().includes(value)) {
      this.engineers.update((list) => [...list, value]);
    }
    event.chipInput?.clear();
  }

  removeEngineer(id: string): void {
    this.engineers.update((list) => list.filter((e) => e !== id));
  }

  enable(): void {
    if (this.form.invalid || this.engineers().length === 0) {
      this.form.markAllAsTouched();
      return;
    }
    this.submitting.set(true);
    this.admin
      .enableSupportChannel({
        engineerIdentifiers: this.engineers(),
        durationMinutes: this.form.controls.durationMinutes.value,
      })
      .subscribe({
        next: (s) => {
          this.status.set(s);
          this.engineers.set([]);
          this.notify.success(
            this.i18n.translate('administration.support.enabledToast'),
          );
          this.submitting.set(false);
        },
        error: () => this.submitting.set(false),
      });
  }

  revoke(): void {
    const data: ConfirmDialogData = {
      title: this.i18n.translate('administration.support.revokeTitle'),
      message: this.i18n.translate('administration.support.revokeMessage'),
      confirmLabel: this.i18n.translate('administration.support.revokeConfirm'),
      destructive: true,
    };
    this.dialog
      .open(ConfirmDialogComponent, { data, width: '440px' })
      .afterClosed()
      .subscribe((confirmed) => {
        if (!confirmed) {
          return;
        }
        this.admin.revokeSupportChannel().subscribe({
          next: () => {
            this.notify.success(
              this.i18n.translate('administration.support.revokedToast'),
            );
            this.fetch();
          },
        });
      });
  }
}
